using DragonBot.Context;
using DragonBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DragonBot
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();

            services.AddDbContext<DragonContext>(options =>
            {
                options.UseSqlite("Data Source=C:\\Users\\sjors\\Desktop\\DragonBot\\Dragon.db");
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            });

            services.AddScoped<IClanMemberService, ClanMemberService>();
            services.AddScoped<IPointDistributionService, PointDistributionService>();
            services.AddScoped<IVouchService, VouchService>();

            var serviceProvider = services.BuildServiceProvider();

            var bot = new Bot(serviceProvider,
                serviceProvider.GetRequiredService<IClanMemberService>(),
                serviceProvider.GetRequiredService<IPointDistributionService>(),
                serviceProvider.GetRequiredService<IVouchService>());
            services.AddSingleton(bot);
            await bot.RunAsync();
        }
    }
}