namespace Wolfrender.Blazor.Raylib.Services;

public static class RegisterServices
{
    public static void AddRaylibServices(this IServiceCollection services)
    {
        services.AddScoped<ResourceService>();
    }
}
