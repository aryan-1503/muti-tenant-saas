using FluentValidation;
using MediatR;

namespace InventoryManager.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behaviour that runs FluentValidation before every handler.
/// This means you NEVER call .Validate() manually in a handler — it just happens.
///
/// HOW IT WORKS:
/// MediatR's pipeline: Request → [ValidationBehaviour] → [Handler] → Response
///
/// If any validator returns failures, a ValidationException is thrown here
/// and the handler is never called. GlobalExceptionMiddleware catches it and
/// returns HTTP 400 with the validation errors.
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();  // No validators registered for this request — proceed

        var context = new ValidationContext<TRequest>(request);

        // Run all validators in parallel for performance
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
