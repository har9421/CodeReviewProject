# Use the official .NET 8 runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copy the published application
FROM base AS final
WORKDIR /app
COPY dist/ .

# Create logs directory
RUN mkdir -p logs

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:80/api/webhook/health || exit 1

ENTRYPOINT ["dotnet", "CodeReviewBot.dll"]
