using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Categories.UpdateCategory;

// ─── Command ──────────────────────────────────────────────────────────────────
public record UpdateCategoryCommand(Guid Id, string Name, string? Description) : IRequest;

// ─── Validator ────────────────────────────────────────────────────────────────
public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateCategoryCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is Domain.Enums.UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot update categories.");

        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Category not found.");

        var nameConflict = await _db.Categories
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower().Trim()
                        && c.Id != request.Id, cancellationToken);

        if (nameConflict)
            throw new InvalidOperationException($"A category named '{request.Name}' already exists.");

        category.Name = request.Name.Trim();
        category.Description = request.Description?.Trim();

        await _db.SaveChangesAsync(cancellationToken);
    }
}
