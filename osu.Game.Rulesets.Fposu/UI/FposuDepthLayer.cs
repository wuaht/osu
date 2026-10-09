// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Game.Configuration;
using osu.Game.Rulesets.Fposu.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Fposu.UI
{
    /// <summary>
    /// Displays the objects of the playfield in 3D in front of (or behind) the <see cref="FposuScreen"/> with <see cref="FposuModDepth"/>,
    /// instead of on the screen, so that they actually come towards the camera and aren't cut off by the edges of the screen.
    /// </summary>
    /// <remarks>
    /// The objects keep their regular 2D layout (so that they are hit as usual) and are drawn through proxies with a perspective projection.
    /// Each object is a plane at its depth, parallel to the screen at the point which is aimed at (the centre of circles and the ball of sliders),
    /// so that this point is exactly where it is on the screen when the object reaches the screen.
    /// </remarks>
    public partial class FposuDepthLayer : CompositeDrawable
    {
        /// <summary>
        /// The distance between points used to determine the orientation of the screen, in pixels of the 2D game.
        /// </summary>
        private const float tangent_step = 10;

        /// <summary>
        /// The minimum distance of an object in front of the camera, relative to the distance of the screen, for it to be displayed.
        /// </summary>
        private const float min_relative_view_depth = 0.05f;

        /// <summary>
        /// The factor by which the projections are scaled (which doesn't change the projected positions), so that the w coordinate of projected vertices is large enough.
        /// The framework sets the z coordinate of vertices to their draw depth after the projection, so vertices with w smaller than that (e.g. at the sides of the view) would be clipped.
        /// </summary>
        private const float projection_scale = 1000;

        private static readonly FieldInfo? proxy_field = typeof(Drawable).GetField("proxy", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly FposuScreen screen;

        private FposuModDepth? mod;
        private Playfield? playfield;

        private Bindable<float> fov = null!;

        /// <summary>
        /// The displayed objects by the drawable they display (a hit object, or an approach circle).
        /// </summary>
        private readonly Dictionary<Drawable, DepthObject> objects = new Dictionary<Drawable, DepthObject>();

        /// <summary>
        /// The projections of the hit objects in the current frame.
        /// </summary>
        private readonly Dictionary<DrawableHitObject, (Matrix4 projection, float viewDepth)> projections = new Dictionary<DrawableHitObject, (Matrix4, float)>();

        private DepthObject? cursor;

        public FposuDepthLayer(FposuScreen screen)
        {
            this.screen = screen;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            fov = config.GetBindable<float>(OsuSetting.SlopFposuFov);
        }

        /// <summary>
        /// Starts displaying the objects of a playfield in 3D.
        /// </summary>
        public void Apply(FposuModDepth depthMod, Playfield objectPlayfield)
        {
            mod = depthMod;
            playfield = objectPlayfield;
        }

        protected override void Update()
        {
            base.Update();

            foreach (var o in objects.Values)
                o.Alpha = 0;

            projections.Clear();

            if (cursor != null)
                cursor.Alpha = 0;

            if (mod == null || playfield == null || !screen.IsLoaded || screen.DrawWidth <= 0)
                return;

            foreach (var entry in playfield.HitObjectContainer.AliveEntries)
            {
                var drawable = entry.Value;

                if (mod.GetDepth(drawable) is not float depth || !TryGetProjection(drawable, depth, out var projection, out float viewDepth))
                    continue;

                projections[drawable] = (projection, viewDepth);

                display(drawable, projection, viewDepth);

                // approach circles are displayed in a separate layer of the playfield, which they are taken from as well.
                var circle = drawable as DrawableHitCircle ?? (drawable as DrawableSlider)?.NestedHitObjects.OfType<DrawableSliderHead>().FirstOrDefault();

                // the approach circle is drawn through a proxy of its existing proxy, which the framework considers valid to draw whenever that existing proxy is,
                // even when the approach circle itself isn't drawn anymore (e.g. after it has faded out). that would keep displaying its last state until the object is removed.
                if (circle != null && isPresent(circle.ProxiedLayer))
                {
                    // slightly in front of the object.
                    display(circle.ProxiedLayer, projection, viewDepth * 0.9999f);
                }
            }

            displayCursor();
        }

        /// <summary>
        /// Displays the cursor on the screen in front of all objects (it would otherwise be hidden behind objects in front of the screen).
        /// Only the cursor itself, as its trail and ripples belong to the screen.
        /// </summary>
        private void displayCursor()
        {
            if (playfield?.Cursor?.ActiveCursor is not Drawable activeCursor)
                return;

            if (!tryGetProjection(activeCursor.ScreenSpaceDrawQuad.Centre, 0, out var projection, out _))
                return;

            if (cursor == null)
            {
                if (createProxy(activeCursor) is not Drawable proxy)
                    return;

                AddInternal(cursor = new DepthObject(proxy) { Depth = float.MinValue });
            }

            cursor.Alpha = 1;
            cursor.Projection = projection;
        }

        /// <summary>
        /// Whether a drawable of the playfield is currently drawn, i.e. it and all of its parents are present.
        /// </summary>
        private bool isPresent(Drawable drawable)
        {
            for (Drawable? d = drawable; d != null && d != playfield; d = d.Parent)
            {
                if (!d.IsPresent)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Whether a drawable (a hit object, or an approach circle) is currently displayed in 3D.
        /// </summary>
        public bool IsDisplayed(Drawable drawable) => objects.TryGetValue(drawable, out var o) && o.Alpha > 0;

        private void display(Drawable drawable, Matrix4 projection, float viewDepth)
        {
            if (!objects.TryGetValue(drawable, out var o))
            {
                var proxy = createProxy(drawable);

                if (proxy == null)
                    return;

                AddInternal(o = new DepthObject(proxy));
                objects[drawable] = o;
            }

            o.Alpha = 1;
            o.Projection = projection;

            // further objects are drawn first.
            if (o.Depth != viewDepth)
                ChangeInternalChildDepth(o, viewDepth);
        }

        /// <summary>
        /// Creates a proxy of a drawable, or of its existing proxy (e.g. approach circles, which are proxied into their own layer of the playfield).
        /// </summary>
        private static Drawable? createProxy(Drawable drawable)
        {
            if (!drawable.HasProxy)
                return drawable.CreateProxy();

            // the existing proxy isn't exposed.
            var existingProxy = proxy_field?.GetValue(drawable) as Drawable;

            if (existingProxy == null || existingProxy.HasProxy)
                return null;

            return existingProxy.CreateProxy();
        }

        /// <summary>
        /// Computes the projection of the 2D game to the screen for an object at the given depth.
        /// </summary>
        /// <param name="drawable">The object.</param>
        /// <param name="depth">The depth of the object in osu!pixels behind the playfield.</param>
        /// <param name="projection">The projection from screen space of the 2D game to screen space.</param>
        /// <param name="viewDepth">The distance of the object from the camera along the view direction.</param>
        /// <returns>Whether the object can be displayed (it isn't behind the camera).</returns>
        public bool TryGetProjection(DrawableHitObject drawable, float depth, out Matrix4 projection, out float viewDepth)
        {
            // the point which is aimed at, in screen space of the 2D game.
            Vector2 anchor = drawable is DrawableSlider slider ? slider.Ball.ScreenSpaceDrawQuad.Centre : drawable.ScreenSpaceDrawQuad.Centre;

            return tryGetProjection(anchor, depth, out projection, out viewDepth);
        }

        /// <summary>
        /// Computes the projection of the 2D game to the screen for a plane at the given depth, which is parallel to the screen at the given anchor.
        /// </summary>
        private bool tryGetProjection(Vector2 anchor, float depth, out Matrix4 projection, out float viewDepth)
        {
            projection = Matrix4.Identity;
            viewDepth = 0;

            if (playfield == null)
                return false;

            Vector3 anchorPosition = getScreenPosition(anchor);

            // the orientation and scale of the screen at the anchor, per pixel of the 2D game.
            Vector3 tangentX = (getScreenPosition(anchor + new Vector2(tangent_step, 0)) - getScreenPosition(anchor - new Vector2(tangent_step, 0))) / (2 * tangent_step);
            Vector3 tangentY = (getScreenPosition(anchor + new Vector2(0, tangent_step)) - getScreenPosition(anchor - new Vector2(0, tangent_step))) / (2 * tangent_step);

            // away from the camera, which is at the origin.
            Vector3 normal = Vector3.Cross(tangentX, tangentY).Normalized();
            if (Vector3.Dot(normal, anchorPosition) < 0)
                normal = -normal;

            // osu!pixels to pixels of the 2D game, to units of the 3D environment.
            float pixelsPerUnit = (playfield.ToScreenSpace(new Vector2(1, 0)) - playfield.ToScreenSpace(Vector2.Zero)).Length;
            Vector3 offset = normal * (depth * pixelsPerUnit * tangentX.Length);

            // the position of an arbitrary point of the 2D game in 3D: origin + tangentX * x + tangentY * y.
            Vector3 origin = anchorPosition + offset - tangentX * anchor.X - tangentY * anchor.Y;

            var camera = screen.Camera;

            viewDepth = Vector3.Dot(anchorPosition + offset, camera.Forward);

            if (viewDepth < min_relative_view_depth * screen.Mesh.Distance)
                return false;

            // the same projection as the screen (see FposuScreen).
            var screenQuad = screen.ScreenSpaceDrawQuad.AABBFloat;
            Vector2 centre = screenQuad.Centre;
            float focalLength = screenQuad.Width / 2 / MathF.Tan(MathHelper.DegreesToRadians(fov.Value) / 2);

            // for a point p in 3D, the projected position is (x / w, y / w), with
            //   x = centre.X * dot(p, forward) + focalLength * dot(p, right),
            //   y = centre.Y * dot(p, forward) - focalLength * dot(p, up),
            //   w = dot(p, forward),
            // all of which are linear in p, which is linear in the position in the 2D game. so the projection is a homography.
            float scale = projection_scale / screen.Mesh.Distance;

            Vector4 project(Vector3 p) => new Vector4(
                centre.X * Vector3.Dot(p, camera.Forward) + focalLength * Vector3.Dot(p, camera.Right),
                centre.Y * Vector3.Dot(p, camera.Forward) - focalLength * Vector3.Dot(p, camera.Up),
                0,
                Vector3.Dot(p, camera.Forward)) * scale;

            // vertices are (x, y, z, 1) with z = 1 in 2D, or the depth of paths. row vectors are multiplied with the matrix.
            projection = new Matrix4(
                project(tangentX),
                project(tangentY),
                Vector4.Zero,
                project(origin));

            return true;
        }

        /// <summary>
        /// Returns the position on the screen in 3D of a position in screen space of the 2D game.
        /// </summary>
        private Vector3 getScreenPosition(Vector2 screenSpacePosition) => screen.Mesh.GetPosition(screen.ToUVFromScreenSpace(screenSpacePosition));

        /// <summary>
        /// Displays a drawable (through a proxy) with a projection.
        /// </summary>
        private partial class DepthObject : CompositeDrawable
        {
            public Matrix4 Projection;

            public DepthObject(Drawable proxy)
            {
                RelativeSizeAxes = Axes.Both;
                InternalChild = proxy;
            }

            protected override DrawNode CreateDrawNode() => new DepthObjectDrawNode(this);

            private class DepthObjectDrawNode : CompositeDrawableDrawNode
            {
                protected new DepthObject Source => (DepthObject)base.Source;

                private Matrix4 projection;

                public DepthObjectDrawNode(DepthObject source)
                    : base(source)
                {
                }

                public override void ApplyState()
                {
                    base.ApplyState();
                    projection = Source.Projection;
                }

                protected override void Draw(IRenderer renderer)
                {
                    push(renderer);
                    base.Draw(renderer);
                    pop(renderer);
                }

                // the depth of projected vertices doesn't correspond to their draw depth (see projection_scale), so the depth buffer isn't used.
                // objects are drawn back to front instead, in the regular pass.
                protected override void DrawOpaqueInterior(IRenderer renderer)
                {
                }

                private void push(IRenderer renderer)
                {
                    // scissor rectangles (used for masking) are computed without the projection.
                    renderer.PushScissorState(false);
                    renderer.PushDepthInfo(new DepthInfo(depthTest: false, writeDepth: false));
                    renderer.PushProjectionMatrix(projection * renderer.ProjectionMatrix);
                }

                private static void pop(IRenderer renderer)
                {
                    renderer.PopProjectionMatrix();
                    renderer.PopDepthInfo();
                    renderer.PopScissorState();
                }
            }
        }
    }
}
