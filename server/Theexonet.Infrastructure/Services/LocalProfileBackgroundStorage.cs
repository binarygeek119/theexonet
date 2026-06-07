using Theexonet.Core.Validation;

namespace Theexonet.Infrastructure.Services;

public class LocalProfileBackgroundStorage(ProfileBackgroundStorageOptions options) : IProfileBackgroundStorage
{
    private const string RelativeFolder = ProfileBackgroundStorageOptions.RelativeFolder;

    public async Task<string> SaveAsync(
        Guid playerId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(options.ImagesRootPath, RelativeFolder);
        Directory.CreateDirectory(directory);
        await DeleteForPlayerAsync(playerId, cancellationToken);

        var extension = ProfileAvatarValidator.ExtensionForContentType(contentType);
        var fileName = $"{playerId:N}{extension}";
        var absolutePath = Path.Combine(directory, fileName);

        await using var output = File.Create(absolutePath);
        await content.CopyToAsync(output, cancellationToken);

        return $"/{ProfileBackgroundStorageOptions.PublicUrlPath}/{fileName}";
    }

    public Task DeleteForPlayerAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(options.ImagesRootPath, RelativeFolder);
        if (!Directory.Exists(directory))
        {
            return Task.CompletedTask;
        }

        var prefix = playerId.ToString("N");
        foreach (var path in Directory.EnumerateFiles(directory, $"{prefix}.*"))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
