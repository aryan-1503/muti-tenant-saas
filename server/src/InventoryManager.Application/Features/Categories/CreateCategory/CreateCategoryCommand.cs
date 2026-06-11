using FluentValidation;
using InventoryManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Categories.CreateCategory;

// ─── Command ──────────────────────────────────────────────────────────────────
public record CreateCategoryCommand(string Name, string? Description) : IRequest<Guid>;

// ─── Validator ────────────────────────────────────────────────────────────────
public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateCategoryCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is Domain.Enums.UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot create categories.");

        var nameExists = await _db.Categories
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower().Trim(), cancellationToken);

        if (nameExists)
            throw new InvalidOperationException($"A category named '{request.Name}' already exists.");

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim()
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }
}
