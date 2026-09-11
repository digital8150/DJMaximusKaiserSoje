using System;
using UnityEngine;

namespace DJMaximusKaiserSoje.Presentation
{
    [Serializable]
    public sealed class BattleCrewData
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string roleTitle;
        [SerializeField] private Sprite previewIcon;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] excitedFrames;
        [SerializeField] private Sprite[] badFrames;
        [SerializeField] private float framesPerSecond = 24f;

        public BattleCrewData(
            string id,
            string displayName,
            string roleTitle,
            Sprite previewIcon,
            Sprite[] idleFrames,
            Sprite[] excitedFrames,
            Sprite[] badFrames,
            float framesPerSecond = 24f)
        {
            this.id = id;
            this.displayName = displayName;
            this.roleTitle = roleTitle;
            this.previewIcon = previewIcon;
            this.idleFrames = idleFrames ?? Array.Empty<Sprite>();
            this.excitedFrames = excitedFrames ?? Array.Empty<Sprite>();
            this.badFrames = badFrames ?? Array.Empty<Sprite>();
            this.framesPerSecond = framesPerSecond > 0f ? framesPerSecond : 24f;
        }

        public string Id => id;
        public string DisplayName => displayName;
        public string RoleTitle => roleTitle;
        public Sprite PreviewIcon => previewIcon;
        public Sprite[] IdleFrames => idleFrames ?? Array.Empty<Sprite>();
        public Sprite[] ExcitedFrames => excitedFrames ?? Array.Empty<Sprite>();
        public Sprite[] BadFrames => badFrames ?? Array.Empty<Sprite>();
        public float FramesPerSecond => framesPerSecond;
    }
}
