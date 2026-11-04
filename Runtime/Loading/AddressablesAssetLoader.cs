using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Loreman.Dialogues
{
    /// <summary>Loads assets with Addressables. Every load is counted by Addressables: release what you load.</summary>
    public sealed class AddressablesAssetLoader : IAssetLoader
    {
        public async Task<T> LoadAsync<T>(string address, CancellationToken cancellationToken) where T : Object
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = Addressables.LoadAssetAsync<T>(address);
            try
            {
                await handle.Task;
            }
            catch
            {
                if (handle.IsValid()) Addressables.Release(handle);
                throw;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                var error = handle.OperationException;
                if (handle.IsValid()) Addressables.Release(handle);
                throw new InvalidOperationException($"Loreman: could not load \"{address}\". Is the build imported and Addressables built?", error);
            }

            var result = handle.Result;
            if (cancellationToken.IsCancellationRequested)
            {
                Addressables.Release(handle);
                cancellationToken.ThrowIfCancellationRequested();
            }
            return result;
        }

        public void Release(Object asset)
        {
            if (asset != null) Addressables.Release(asset);
        }
    }
}
