using System.Collections;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EcomAI.Platform.Api.Validation;

public sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var argument in context.ActionArguments.Values.Where(arg => arg is not null))
        {
            var result = await ValidateArgumentAsync(
                argument!,
                context.HttpContext.RequestServices,
                context.HttpContext.RequestAborted);

            failures.AddRange(result);
        }

        if (failures.Count > 0)
        {
            var errors = failures
                .GroupBy(x => x.PropertyName)
                .ToDictionary(
                    g => string.IsNullOrWhiteSpace(g.Key) ? "request" : g.Key,
                    g => g.Select(x => x.ErrorMessage).Distinct().ToArray());

            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors)
            {
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
            return;
        }

        await next();
    }

    private static async Task<IReadOnlyList<FluentValidation.Results.ValidationFailure>> ValidateArgumentAsync(
        object argument,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
        var validatorEnumerableType = typeof(IEnumerable<>).MakeGenericType(validatorType);

        if (serviceProvider.GetService(validatorEnumerableType) is not IEnumerable validators)
        {
            return [];
        }

        var context = new ValidationContext<object>(argument);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validatorObj in validators)
        {
            if (validatorObj is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors.Where(e => e is not null));
        }

        return failures;
    }
}
