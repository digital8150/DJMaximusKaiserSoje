using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace DJMaximusKaiserSoje.Presentation
{
    /// <summary>
    /// Shows artwork that lives in the content catalog rather than in the build. Requests are
    /// superseded, not queued, so scrolling a long list does not leave the wrong jacket behind.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AddressableImage : MonoBehaviour
    {
        [SerializeField] internal Image target;
        [SerializeField] internal Sprite placeholder;
        [SerializeField] internal Color placeholderTint = new Color(1f, 1f, 1f, 0.25f);

        private AsyncOperationHandle<Sprite> handle;
        private string requestedAddress;
        private int requestId;

        public void Show(string address)
        {
            if (target == null) return;
            if (requestedAddress == address && handle.IsValid()) return;

            requestedAddress = address;
            int id = ++requestId;
            ReleaseHandle();
            ShowPlaceholder();

            if (string.IsNullOrWhiteSpace(address)) return;

            handle = Addressables.LoadAssetAsync<Sprite>(address);
            handle.Completed += operation =>
            {
                if (id != requestId || target == null) return;
                if (operation.Status != AsyncOperationStatus.Succeeded || operation.Result == null)
                {
                    ShowPlaceholder();
                    return;
                }

                target.sprite = operation.Result;
                target.color = Color.white;
                target.enabled = true;
            };
        }

        public void Clear()
        {
            requestId++;
            requestedAddress = null;
            ReleaseHandle();
            ShowPlaceholder();
        }

        private void ShowPlaceholder()
        {
            target.sprite = placeholder;
            target.color = placeholder == null ? Color.clear : placeholderTint;
            target.enabled = true;
        }

        private void ReleaseHandle()
        {
            if (!handle.IsValid()) return;
            Addressables.Release(handle);
            handle = default;
        }

        private void OnDestroy() => ReleaseHandle();
    }
}
