namespace Theexonet.Infrastructure.Services;

public class LocalCompanyLogoStorage(CompanyLogoStorageOptions options) : ICompanyLogoStorage
{
    public async Task<string> SaveAsync(Guid mineId, Stream content, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(options.ImagesRootPath, CompanyLogoStorageOptions.RelativeFolder);
        Directory.CreateDirectory(directory);
        await DeleteForMineAsync(mineId, cancellationToken);

        var fileName = $"{mineId:N}.png";
        var absolutePath = Path.Combine(directory, fileName);

        await using var output = File.Create(absolutePath);
        await content.CopyToAsync(output, cancellationToken);

        return $"/{CompanyLogoStorageOptions.PublicUrlPath}/{fileName}";
    }

    public Task DeleteForMineAsync(Guid mineId, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(options.ImagesRootPath, CompanyLogoStorageOptions.RelativeFolder);
        if (!Directory.Exists(directory))
        {
            return Task.CompletedTask;
        }

        var prefix = mineId.ToString("N");
        foreach (var path in Directory.EnumerateFiles(directory, $"{prefix}.*"))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
