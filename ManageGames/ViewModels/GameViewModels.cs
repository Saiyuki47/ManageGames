using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using ManageGames.Services;
using ManageGames.Services.Covers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManageGames.ViewModels;

/// <summary>The game list and the wishlist share one view.</summary>
public class GameListViewModel
{
    public IReadOnlyList<GameListItem> Games { get; set; } = [];
    public string? Search { get; set; }
    public bool IsWishList { get; set; }
}

/// <summary>Add and edit form of a game; <see cref="Id"/> is null while adding.</summary>
public class GameFormViewModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(FieldLimits.NameMaxLength)]
    [Display(Name = "Title")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Console")]
    public int? ConsoleId { get; set; }

    [Range(1, FieldLimits.CopiesMax)]
    [Display(Name = "Copies")]
    public int Copies { get; set; } = 1;

    [Display(Name = "On wishlist")]
    [SuppressMessage("Major Bug", "S6964", Justification = "A checkbox: not posted means not checked.")]
    public bool OnWishList { get; set; }

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<SelectListItem> ConsoleOptions { get; set; } = [];

    /// <summary>The current cover while editing; null without one.</summary>
    [BindNever]
    [ValidateNever]
    public CoverInfo? Cover { get; set; }
}

/// <summary>The page to pick a game's cover from the search results.</summary>
public class CoverPickerViewModel
{
    public int GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string? ConsoleName { get; set; }

    /// <summary>What was searched for: the game's title, unless the user typed something else.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>False while no cover source has API keys; only uploading works then.</summary>
    public bool HasProviders { get; set; }

    public IReadOnlyList<RankedCover> Results { get; set; } = [];
    public IReadOnlyList<string> FailedProviders { get; set; } = [];
}
