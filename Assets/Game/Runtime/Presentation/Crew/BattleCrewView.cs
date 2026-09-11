using System;
using UnityEngine;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    public enum CrewAnimationState
    {
        Idle,
        Excited,
        Bad
    }

    /// <summary>
    /// Animates the deployed Battle Crew sprite sheet.
    /// Handles Idle loop, Excited trigger on 125 combo intervals, and Bad trigger on combo break.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class BattleCrewView : MonoBehaviour
    {
        [SerializeField] internal Image displayImage;
        [SerializeField] internal BattleCrewData crewData;

        private CrewAnimationState currentState = CrewAnimationState.Idle;
        private float frameTimer;
        private int currentFrameIndex;
        private int lastExcitedMilestone;
        private int lastSeenCombo;

        public CrewAnimationState CurrentState => currentState;
        public int CurrentFrameIndex => currentFrameIndex;
        public BattleCrewData CrewData => crewData;
        public int LastExcitedMilestone => lastExcitedMilestone;

        private void Awake()
        {
            if (displayImage == null) displayImage = GetComponent<Image>();
        }

        public void Bind(BattleCrewData data)
        {
            if (displayImage == null) displayImage = GetComponent<Image>();
            crewData = data;
            currentState = CrewAnimationState.Idle;
            currentFrameIndex = 0;
            frameTimer = 0f;
            lastExcitedMilestone = 0;
            lastSeenCombo = 0;
            ApplyCurrentFrame();
        }

        public void NotifyCombo(int currentCombo, int previousCombo)
        {
            lastSeenCombo = currentCombo;

            if (currentCombo == 0)
            {
                lastExcitedMilestone = 0;
                if (previousCombo > 0)
                {
                    PlayBad();
                }
                return;
            }

            int milestone = currentCombo / 125;
            if (milestone > 0 && milestone > lastExcitedMilestone)
            {
                lastExcitedMilestone = milestone;
                PlayExcited();
            }
        }

        public void PlayIdle()
        {
            currentState = CrewAnimationState.Idle;
            currentFrameIndex = 0;
            frameTimer = 0f;
            ApplyCurrentFrame();
        }

        public void PlayExcited()
        {
            currentState = CrewAnimationState.Excited;
            currentFrameIndex = 0;
            frameTimer = 0f;
            ApplyCurrentFrame();
        }

        public void PlayBad()
        {
            currentState = CrewAnimationState.Bad;
            currentFrameIndex = 0;
            frameTimer = 0f;
            ApplyCurrentFrame();
        }

        public void UpdateAnimation(float deltaTime)
        {
            if (crewData == null) return;
            Sprite[] frames = GetFramesForState(currentState);
            if (frames == null || frames.Length == 0) return;

            float frameDuration = 1f / Mathf.Max(1f, crewData.FramesPerSecond);
            frameTimer += deltaTime;

            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                currentFrameIndex++;

                if (currentFrameIndex >= frames.Length)
                {
                    if (currentState == CrewAnimationState.Idle)
                    {
                        currentFrameIndex = 0;
                    }
                    else
                    {
                        currentState = CrewAnimationState.Idle;
                        currentFrameIndex = 0;
                        frames = GetFramesForState(currentState);
                        if (frames == null || frames.Length == 0) break;
                    }
                }
            }

            ApplyCurrentFrame();
        }

        private void Update()
        {
            UpdateAnimation(Time.deltaTime);
        }

        public Sprite[] GetFramesForState(CrewAnimationState state)
        {
            if (crewData == null) return null;
            switch (state)
            {
                case CrewAnimationState.Excited: return crewData.ExcitedFrames;
                case CrewAnimationState.Bad: return crewData.BadFrames;
                default: return crewData.IdleFrames;
            }
        }

        private void ApplyCurrentFrame()
        {
            if (displayImage == null || crewData == null) return;
            Sprite[] frames = GetFramesForState(currentState);
            if (frames != null && currentFrameIndex >= 0 && currentFrameIndex < frames.Length)
            {
                displayImage.sprite = frames[currentFrameIndex];
            }
        }
    }
}
