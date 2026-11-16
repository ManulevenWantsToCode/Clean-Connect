using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Clean_Connect.Application.Interface.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Clean_Connect.Application.Behaviours
{
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var (status, title, detail) = Classify(exception);
            if (status >= 500)
                _logger.LogError(exception, "Unhandled request failure. CorrelationId={CorrelationId}", httpContext.Items["X-Correlation-ID"]);
            else
                _logger.LogWarning("Request rejected with status {StatusCode} ({ExceptionType}). CorrelationId={CorrelationId}",
                    status, exception.GetType().Name, httpContext.Items["X-Correlation-ID"]);

            var problemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };
            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
            if (httpContext.Items["X-Correlation-ID"] is string correlationId)
                problemDetails.Extensions["correlationId"] = correlationId;
            if (exception is ValidationException validation)
                problemDetails.Extensions["errors"] = validation.Errors
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.PropertyName) ? "request" : x.PropertyName)
                    .ToDictionary(group => group.Key, _ => new[] { "The supplied value is invalid." });

            httpContext.Response.StatusCode = status;
            httpContext.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(httpContext.Response.Body, problemDetails,
                cancellationToken: cancellationToken);

            return true;
        }

        private static (int Status, string Title, string Detail) Classify(Exception exception) => exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", "One or more request values are invalid."),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request", "The request could not be accepted."),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden", "You are not permitted to perform this operation."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found", "The requested resource was not found."),
            BookingConflictException or IdempotencyConflictException or PaymentInitializationConflictException
                or DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict", "The request conflicts with the current resource state."),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error", "The request could not be completed. Contact support with the correlation ID.")
        };
    }
}
