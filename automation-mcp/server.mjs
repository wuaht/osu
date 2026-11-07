#!/usr/bin/env node
// MCP server (stdio) that builds and launches a dev instance of this client with the automation bridge enabled
// (osu.Game/Automation/AutomationBridge.cs) and exposes it as tools: screenshot, UI tree, click, keys, text.
// Input is injected into the game itself, so the user's mouse and keyboard are never touched.
// No dependencies: speaks newline-delimited JSON-RPC as defined by the MCP stdio transport.

import { spawn, spawnSync } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { createServer } from 'node:net';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import readline from 'node:readline';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
// separate output folder so a dev build the user is running doesn't lock the files.
const outputDir = path.join(repoRoot, 'osu.Desktop', 'bin', 'Automation');
const isWindows = process.platform === 'win32';

let game = null; // { process, port, token }
let lastScale = 1;
const logLines = [];

function log(line) {
    logLines.push(line);
    if (logLines.length > 400) logLines.shift();
}

function freePort() {
    return new Promise((resolve, reject) => {
        const server = createServer();
        server.on('error', reject);
        server.listen(0, '127.0.0.1', () => {
            const { port } = server.address();
            server.close(() => resolve(port));
        });
    });
}

async function bridge(endpoint, body = {}, { raw = false, timeout = 15000 } = {}) {
    if (!game) throw new Error('The client is not running. Call osu_launch first.');

    const response = await fetch(`http://127.0.0.1:${game.port}${endpoint}`, {
        method: 'POST',
        headers: { 'X-Automation-Token': game.token, 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
        signal: AbortSignal.timeout(timeout),
    });

    if (raw && response.ok) return response;

    const json = await response.json();
    if (!response.ok) throw new Error(json.error ?? `HTTP ${response.status}`);
    return json;
}

function runBuild() {
    return new Promise((resolve) => {
        const build = spawn('dotnet', ['build', 'osu.Desktop', '-c', 'Debug', '-nologo', '-v', 'q', '-o', outputDir], { cwd: repoRoot });
        const output = [];
        const collect = (data) => output.push(...data.toString().split(/\r?\n/).filter(Boolean));
        build.stdout.on('data', collect);
        build.stderr.on('data', collect);
        build.on('close', (code) => resolve({ ok: code === 0, output }));
    });
}

async function launch({ build = true }) {
    if (game) {
        try {
            return { alreadyRunning: true, ...(await bridge('/state')) };
        } catch {
            await stop();
        }
    }

    if (build) {
        const result = await runBuild();
        if (!result.ok) {
            const errors = result.output.filter((l) => / error /i.test(l));
            throw new Error(`Build failed:\n${(errors.length ? errors : result.output).slice(-30).join('\n')}`);
        }
    }

    const exe = path.join(outputDir, isWindows ? 'osu!.exe' : 'osu!');
    const [command, args] = existsSync(exe) ? [exe, []] : ['dotnet', [path.join(outputDir, 'osu!.dll')]];

    const port = await freePort();
    const token = randomBytes(24).toString('hex');
    logLines.length = 0;

    const child = spawn(command, args, {
        cwd: outputDir,
        env: {
            ...process.env,
            SLOP_AUTOMATION_PORT: String(port),
            SLOP_AUTOMATION_TOKEN: token,
            // allows running next to a dev build the user already has open.
            SLOP_ALLOW_MULTIPLE_INSTANCES: '1',
        },
    });

    child.stdout.on('data', (d) => d.toString().split(/\r?\n/).filter(Boolean).forEach(log));
    child.stderr.on('data', (d) => d.toString().split(/\r?\n/).filter(Boolean).forEach(log));

    game = { process: child, port, token };
    let exited = false;
    child.on('exit', (code) => {
        exited = true;
        log(`[process exited with code ${code}]`);
        if (game?.process === child) game = null;
    });

    const deadline = Date.now() + 180000;

    while (Date.now() < deadline) {
        if (exited) throw new Error(`The client exited during startup. Last output:\n${logLines.slice(-30).join('\n')}`);

        try {
            return await bridge('/state', {}, { timeout: 2000 });
        } catch {
            await new Promise((r) => setTimeout(r, 500));
        }
    }

    throw new Error('The client did not open the automation bridge within 180 seconds.');
}

async function stop() {
    if (!game) return { stopped: false, reason: 'not running' };

    const child = game.process;
    const exited = new Promise((resolve) => child.once('exit', resolve));

    try {
        await bridge('/exit', {}, { timeout: 3000 });
    } catch {
        // fall through to killing it.
    }

    const graceful = await Promise.race([exited.then(() => true), new Promise((r) => setTimeout(() => r(false), 15000))]);

    if (!graceful) {
        if (isWindows) spawnSync('taskkill', ['/pid', String(child.pid), '/T', '/F']);
        else child.kill('SIGKILL');
    }

    game = null;
    return { stopped: true, graceful };
}

// All coordinates the tools exchange are in the pixel space of the last screenshot.
const toWindow = ({ id, x, y, ...rest }) =>
    id !== undefined ? { id, ...rest } : { x: x / lastScale, y: y / lastScale, ...rest };

const text = (value) => ({ content: [{ type: 'text', text: typeof value === 'string' ? value : JSON.stringify(value, null, 2) }] });

const positionProperties = {
    id: { type: 'integer', description: 'Element id from the last osu_ui_tree call (preferred).' },
    x: { type: 'number', description: 'X in the last screenshot\'s pixel space (when no id is given).' },
    y: { type: 'number', description: 'Y in the last screenshot\'s pixel space (when no id is given).' },
};

const tools = [
    {
        name: 'osu_launch',
        description: 'Build (Debug) and start a dev instance of the osu! client with automation enabled. It runs muted in its own window, on the second monitor and moved to another virtual desktop (if they exist), and is controlled without touching the real mouse/keyboard. Returns the current state.',
        inputSchema: { type: 'object', properties: { build: { type: 'boolean', description: 'Build before launching (default true).' } } },
        run: async (args) => text(await launch(args)),
    },
    {
        name: 'osu_stop',
        description: 'Exit the automated client (gracefully, falling back to killing it).',
        inputSchema: { type: 'object', properties: {} },
        run: async () => text(await stop()),
    },
    {
        name: 'osu_state',
        description: 'Current screen, window size, focused drawable and whether the window is active.',
        inputSchema: { type: 'object', properties: {} },
        run: async () => text(await bridge('/state')),
    },
    {
        name: 'osu_screenshot',
        description: 'Screenshot of the game (rendered frame, works while the window is in the background). Coordinates used by other tools refer to this image.',
        inputSchema: { type: 'object', properties: { maxWidth: { type: 'integer', description: 'Downscale to at most this width (default 1280).' } } },
        run: async ({ maxWidth = 1280 }) => {
            const response = await bridge('/screenshot', { maxWidth }, { raw: true });
            lastScale = parseFloat(response.headers.get('x-scale') ?? '1');
            const data = Buffer.from(await response.arrayBuffer()).toString('base64');
            return { content: [{ type: 'image', data, mimeType: 'image/png' }] };
        },
    },
    {
        name: 'osu_ui_tree',
        description: 'List visible UI elements: interactive drawables (buttons, text boxes, panels; marked *) with their texts, plus loose texts. Each gets an id usable with osu_click/osu_hover/osu_scroll until the next call. Rects are in screenshot pixel space.',
        inputSchema: { type: 'object', properties: { query: { type: 'string', description: 'Only elements whose text or type contains this (case-insensitive).' } } },
        run: async ({ query }) => {
            const nodes = await bridge('/tree', { query });
            const s = lastScale;
            const lines = nodes.map((n) => `#${n.id} ${n.type}${n.interactive ? '*' : ''} ${n.text ? JSON.stringify(n.text) + ' ' : ''}@ ${Math.round(n.x * s)},${Math.round(n.y * s)} ${Math.round(n.w * s)}x${Math.round(n.h * s)}`);
            return text(lines.length ? lines.join('\n') : 'No matching elements.');
        },
    },
    {
        name: 'osu_click',
        description: 'Click an element (by id) or a position.',
        inputSchema: {
            type: 'object',
            properties: {
                ...positionProperties,
                button: { type: 'string', enum: ['left', 'right'], description: 'Default left.' },
                double: { type: 'boolean', description: 'Double click.' },
            },
        },
        run: async (args) => text(await bridge('/click', toWindow(args))),
    },
    {
        name: 'osu_hover',
        description: 'Move the in-game cursor to an element or position (shows tooltips / hover states).',
        inputSchema: { type: 'object', properties: positionProperties },
        run: async (args) => text(await bridge('/hover', toWindow(args))),
    },
    {
        name: 'osu_scroll',
        description: 'Scroll with the cursor over an element or position.',
        inputSchema: {
            type: 'object',
            properties: { ...positionProperties, amount: { type: 'number', description: 'Wheel notches; negative scrolls down (default -1).' } },
        },
        run: async (args) => text(await bridge('/scroll', toWindow(args))),
    },
    {
        name: 'osu_key',
        description: 'Press a key or combination, using osuTK Key names joined by "+", e.g. "Escape", "Enter", "F2", "ControlLeft+O", "ShiftLeft+Tab", "Up".',
        inputSchema: { type: 'object', properties: { combo: { type: 'string' } }, required: ['combo'] },
        run: async ({ combo }) => text(await bridge('/key', { combo })),
    },
    {
        name: 'osu_type',
        description: 'Type text into the focused text box (click it first). Use osu_key for Enter etc.',
        inputSchema: { type: 'object', properties: { text: { type: 'string' } }, required: ['text'] },
        run: async (args) => text(await bridge('/type', { text: args.text })),
    },
    {
        name: 'osu_logs',
        description: 'Last lines of the client\'s console output.',
        inputSchema: { type: 'object', properties: { lines: { type: 'integer', description: 'Default 50.' } } },
        run: async ({ lines = 50 }) => text(logLines.slice(-lines).join('\n') || '(no output)'),
    },
];

function send(message) {
    process.stdout.write(JSON.stringify(message) + '\n');
}

async function onMessage(message) {
    const { id, method, params } = message;
    if (id === undefined) return; // notifications need no answer.

    try {
        switch (method) {
            case 'initialize':
                return send({
                    jsonrpc: '2.0', id,
                    result: {
                        protocolVersion: params?.protocolVersion ?? '2025-06-18',
                        capabilities: { tools: {} },
                        serverInfo: { name: 'osu-automation', version: '1.0.0' },
                    },
                });

            case 'ping':
                return send({ jsonrpc: '2.0', id, result: {} });

            case 'tools/list':
                return send({ jsonrpc: '2.0', id, result: { tools: tools.map(({ run, ...t }) => t) } });

            case 'tools/call': {
                const tool = tools.find((t) => t.name === params?.name);
                if (!tool) return send({ jsonrpc: '2.0', id, error: { code: -32602, message: `Unknown tool ${params?.name}` } });

                try {
                    return send({ jsonrpc: '2.0', id, result: await tool.run(params.arguments ?? {}) });
                } catch (e) {
                    return send({ jsonrpc: '2.0', id, result: { ...text(String(e.message ?? e)), isError: true } });
                }
            }

            default:
                return send({ jsonrpc: '2.0', id, error: { code: -32601, message: `Method not found: ${method}` } });
        }
    } catch (e) {
        send({ jsonrpc: '2.0', id, error: { code: -32603, message: String(e.message ?? e) } });
    }
}

readline.createInterface({ input: process.stdin }).on('line', (line) => {
    if (!line.trim()) return;

    let message;
    try {
        message = JSON.parse(line);
    } catch {
        return send({ jsonrpc: '2.0', id: null, error: { code: -32700, message: 'Parse error' } });
    }

    onMessage(message);
}).on('close', async () => {
    // the Claude session ended: don't leave the client running.
    await stop().catch(() => {});
    process.exit(0);
});
