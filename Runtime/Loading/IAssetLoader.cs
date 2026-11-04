using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Loreman.Dialogues
{
    /// <summary>
    /// Loads assets by address and lets them go. The package loads through this door only, so Addressables is one implementation
    /// (<see cref="AddressablesAssetLoader"/>) and a test can put its own.
    /// </summary>
    public interface IAssetLoader
    {
        Task<T> LoadAsync<T>(string address, CancellationToken cancellationToken) where T : Object;

        /// <summary>The addresses of the assets that have a label (nothing is loaded).</summary>
        Task<IReadOnlyList<string>> FindAddressesAsync(string label, CancellationToken cancellationToken);

        /// <summary>Lets an asset go (it was loaded by <see cref="LoadAsync{T}"/>). The asset is unloaded when nobody else holds it.</summary>
        void Release(Object asset);
    }
}
