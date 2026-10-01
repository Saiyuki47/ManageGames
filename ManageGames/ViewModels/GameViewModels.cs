using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using ManageGames.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManageGames.ViewModels;

/// <summary>The game list and the wishlist share one view.</summary>
public class GameListViewModel
{
    public IReadOnlyList<Game> Games { get; set; } = [];
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
}
