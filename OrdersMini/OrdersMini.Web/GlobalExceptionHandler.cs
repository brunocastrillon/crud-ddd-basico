using Microsoft.AspNetCore.Diagnostics;

namespace OrdersMini.Web
{
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IProblemDetailsService _problemDetailsService;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService)
        {
            _logger = logger;
            _problemDetailsService = problemDetailsService;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Unhandled exception");

            int status = exception switch
            {
                ArgumentException => StatusCodes.Status400BadRequest,
                TimeoutException => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            };

            httpContext.Response.StatusCode = status;

            // Envia ProblemDetails padronizado
            await _problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails =
                {
                    Title  = "An unexpected error occurred.",
                    Detail = httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() ? exception.Message : null,
                    Status = status,
                    Type   = $"https://httpstatuses.io/{status}"
                },
                Exception = exception
            });

            return true; // indica que a exceção foi tratada
        }
    }
}