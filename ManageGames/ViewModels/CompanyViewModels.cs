using System.ComponentModel.DataAnnotations;
using ManageGames.Models;

namespace ManageGames.ViewModels;

public class CompanyListViewModel
{
    public List<Company> Companies { get; set; } = [];
    public string? Search { get; set; }
}

/// <summary>Add and edit form of a company; <see cref="Id"/> is null while adding.</summary>
public class CompanyFormViewModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(FieldLimits.NameMaxLength)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;
}
