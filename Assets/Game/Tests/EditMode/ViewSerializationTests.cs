using System;
using System.Collections.Generic;
using System.Linq;
using DJMaximusKaiserSoje.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    /// <summary>
    /// Unity only creates a MonoScript for the type a script file is named after. A view sharing a
    /// file with another one can still be added in memory, so a screen builder looks like it worked —
    /// and then the component is dropped the moment the scene is written. Nothing reports it.
    /// </summary>
    public sealed class ViewSerializationTests
    {
        private const string PresentationFolder = "Assets/Game/Runtime/Presentation";

        [Test]
        public void EveryView_CanBeSavedIntoAScene()
        {
            HashSet<Type> serializable = ScriptedTypes();
            var orphans = new List<string>();

            foreach (Type type in typeof(PulsingGraphic).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
                if (!serializable.Contains(type)) orphans.Add(type.FullName);
            }

            Assert.That(orphans, Is.Empty,
                "These views have no script file of their own, so a built scene loses them: " +
                string.Join(", ", orphans));
        }

        private static HashSet<Type> ScriptedTypes()
        {
            var types = new HashSet<Type>();
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { PresentationFolder }))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                Type type = script == null ? null : script.GetClass();
                if (type != null) types.Add(type);
            }

            return types;
        }
    }
}
