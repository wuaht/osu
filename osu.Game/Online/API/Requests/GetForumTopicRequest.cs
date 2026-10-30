// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// Gets the first post of a forum topic.
    /// </summary>
    public class GetForumTopicRequest : APIRequest<APIForumTopic>
    {
        public readonly long ID;

        public GetForumTopicRequest(long id)
        {
            ID = id;
        }

        protected override string Target => $@"forums/topics/{ID}?limit=1";
    }
}
