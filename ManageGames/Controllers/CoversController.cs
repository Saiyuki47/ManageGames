using ManageGames.Auth;
using ManageGames.Models;
using ManageGames.Services;
using ManageGames.Services.Covers;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ManageGames.Controllers;

/// <summary>
/// The covers of the signed-in user's games: showing them, picking one from the search results, uploading one
/// and removing it.
/// </summary>
[Authorize]
public class CoversController(GameService games, CoverService covers, CoverScraper scraper) : Controller
{
    // The multipart request around the file is a little larger than the file itself.
    private const int UploadRequestLimit = CoverImage.MaxBytes + (64 * 1024);

    /// <summary>The cover image. Its address contains the cover's version, so browsers may keep it for good.</summary>
    [HttpGet]
    public async Task<IActionResult> Image(int id, string? v, CancellationToken cancellationToken)
    {
        var cover = await covers.GetCoverAsync(id, User.GetUserId(), cancellationToken);
        if (cover == null)
        {
            return NotFound();
        }

        var version = cover.Version.ToString("N");
        Response.Headers.CacheControl = string.Equals(v, version, StringComparison.Ordinal)
            ? "private, max-age=31536000, immutable"
            : "private, no-cache";
        return File(cover.Data, cover.ContentType, lastModified: null, new EntityTagHeaderValue($"\"{version}\""));
    }

    /// <summary>Searches all cover sources and shows the results to pick from.</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id, string? query, CancellationToken cancellationToken)
    {
        var game = await games.GetGameAsync(id, User.GetUserId(), cancellationToken);
        if (game == null)
        {
            return NotFound();
        }

        var console = game.Console?.Name;
        var model = new CoverPickerViewModel
        {
            GameId = game.Id,
            GameName = game.Name,
            ConsoleName = console,
            Query = string.IsNullOrWhiteSpace(query) ? game.Name : query.Trim(),
            HasProviders = scraper.GetActiveProviders().Count > 0,
        };
        if (model.HasProviders)
        {
            var result = await scraper.SearchAsync(model.Query, console, cancellationToken);
            model.Results = result.Covers;
            model.FailedProviders = result.FailedProviders;
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Choose(int id, string? provider, string? imageUrl, CancellationToken cancellationToken)
    {
        switch (await covers.ChooseCoverAsync(id, User.GetUserId(), provider, imageUrl, cancellationToken))
        {
            case CoverChoiceResult.GameNotFound:
                return NotFound();
            case CoverChoiceResult.NotAllowed:
                return BadRequest();
            case CoverChoiceResult.DownloadFailed:
                TempData[TempDataKeys.Error] = "The cover could not be downloaded. Please try another one.";
                return RedirectToAction(nameof(Edit), new { id });
            default:
                TempData[TempDataKeys.Message] = "Cover saved.";
                return RedirectToGame(id);
        }
    }

    [HttpPost]
    [RequestSizeLimit(UploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<IActionResult> Upload(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        var image = file is { Length: > 0 and <= CoverImage.MaxBytes } ? CoverImage.FromBytes(await ReadAsync(file, cancellationToken)) : null;
        if (image == null)
        {
            // The edit page itself answers 404 for someone else's game.
            TempData[TempDataKeys.Error] = "Please choose a JPEG, PNG, WebP or GIF image of at most 5 MB.";
            return RedirectToGame(id);
        }

        if (!await covers.SetCoverAsync(id, User.GetUserId(), image, GameCover.UploadSource, cancellationToken))
        {
            return NotFound();
        }
        TempData[TempDataKeys.Message] = "Cover saved.";
        return RedirectToGame(id);
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        if (!await covers.RemoveCoverAsync(id, User.GetUserId(), cancellationToken))
        {
            return NotFound();
        }
        TempData[TempDataKeys.Message] = "Cover removed.";
        return RedirectToGame(id);
    }

    /// <summary>
    /// Passes a search result's preview image through, so the browser only ever talks to this app (the content
    /// security policy allows no other image sources). Only fetches images of the active cover sources.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Preview(string? url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || !scraper.IsKnownImage(address))
        {
            return BadRequest();
        }

        var image = await scraper.DownloadAsync(address, cancellationToken);
        if (image == null)
        {
            return NotFound();
        }
        Response.Headers.CacheControl = "private, max-age=86400";
        return File(image.Data, image.ContentType);
    }

    private RedirectToActionResult RedirectToGame(int id)
    {
        return RedirectToAction(nameof(GamesController.Edit), "Games", new { id });
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
