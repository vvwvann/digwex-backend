using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Digwex.Data;
using Digwex.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.IO;
using Digwex.Helpers;
using Microsoft.Extensions.FileProviders;
using Digwex.Services;
using Digwex.Middlewares;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Digwex.Extensions;
using FFMpegCore;
using Microsoft.AspNetCore.StaticFiles;

namespace Digwex
{
  public class Startup
  {
    public static string JWT_SECRET_KEY;
    private readonly SettingsModel _appConfig;
    public static string STORAGE_PATH;
    internal static string TMP_PATH;
    public static readonly string CONTENT_REQUEST = "content";

    public static SetupEntity SetupModel;


    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
      JWT_SECRET_KEY = configuration["JwtSecret"];

      _appConfig = new SettingsModel();
      Configuration.GetSection("Settings")
        .Bind(_appConfig);

      if (Program.SERVICE_MODE) {
        _appConfig.StoragePath = AppContext.BaseDirectory + "../storage";
        _appConfig.FFmpegPath = AppContext.BaseDirectory + "../ffmpeg/bin";
      }
      if (Program.DEVELOP_MODE) {
        _appConfig.FFmpegPath = "D:/develop/ffmpeg/bin";
        _appConfig.StoragePath = "D:/storage/Digwex";
      }

      Log.Info("App config:\n" + _appConfig.JsonToString(Newtonsoft.Json.Formatting.Indented));

      FFMpegOptions.Configure(new FFMpegOptions {
        RootDirectory = _appConfig.FFmpegPath
      });
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      string defaultConnection = Configuration.GetConnectionString("DefaultConnection");
      Log.Info("BaseDirectory: " + AppContext.BaseDirectory);
      Log.Info("DefaultConnection: " + defaultConnection);

      services.AddDbContext<ApplicationDbContext>(options =>
          options.UseNpgsql(defaultConnection));

      services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
          .AddEntityFrameworkStores<ApplicationDbContext>();

      //services.AddIdentityServer()
      //    .AddApiAuthorization<ApplicationUser, ApplicationDbContext>();

      services.AddRouting(options => options.LowercaseUrls = true);

      services.AddSwaggerGen(c => {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
          Description = @"JWT Authorization header using the Bearer scheme. 
                      Enter 'Bearer' [space] and then your token in the text input below.
                      Example: 'Bearer 12345abcdef'",
          Name = "Authorization",
          In = ParameterLocation.Header,
          Type = SecuritySchemeType.ApiKey,
          Scheme = "Bearer"
        });

        c.IgnoreObsoleteProperties();

        c.AddSecurityRequirement(new OpenApiSecurityRequirement()
          {
        {
          new OpenApiSecurityScheme
          {
            Reference = new OpenApiReference
              {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
              },
              Scheme = "oauth2",
              Name = "Bearer",
              In = ParameterLocation.Header,
            },
            new List<string>()
          }
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
        //c.OperationFilter<FileUploadOperation>(); //Register File Upload O
      });
      services.AddSwaggerGenNewtonsoftSupport();



      services.AddAuthentication(x => {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
      }).AddJwtBearer(x => {
        x.RequireHttpsMetadata = false;
        x.SaveToken = true;
        x.TokenValidationParameters = new TokenValidationParameters {
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(JWT_SECRET_KEY)),
          ValidateIssuer = false,
          ValidateAudience = false
        };
      });


      services.AddControllers()
        .AddNewtonsoftJson(x => {
          x.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
          x.SerializerSettings.DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.IgnoreAndPopulate;
        });

      services.AddWebSocketServices();

      services.AddSpaStaticFiles(configuration => {
        configuration.RootPath = "wwwroot";
      });

      // In production, the React files will be served from this directory
      //services.AddSpaStaticFiles(configuration => {
      //  configuration.RootPath = "ClientApp/build";
      //});
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
      Log.Debug("EnvironmentName: " + env.EnvironmentName);
      if (env.IsDevelopment()) {
        app.UseDeveloperExceptionPage();
        app.UseDatabaseErrorPage();
      }
      else {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
      }

      Migrate(serviceProvider);
      SetSetup(serviceProvider);

      STORAGE_PATH = _appConfig.StoragePath;
      TMP_PATH = STORAGE_PATH + "/tmp";

      FileUtils.TryExistOrCreate(STORAGE_PATH);
      FileUtils.TryExistOrCreate(STORAGE_PATH + "/tmp");
      FileUtils.TryExistOrCreate(STORAGE_PATH + "/thumb");
      FileUtils.TryExistOrCreate(STORAGE_PATH + "/files");
      FileUtils.TryExistOrCreate(STORAGE_PATH + "/logs");
      FileUtils.TryExistOrCreate(STORAGE_PATH + "/screens");
      FileUtils.TryExistOrCreate(STORAGE_PATH + "/dist");

      app.UseCors(builder => builder.AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
      app.UseSwagger();

      app.UseStaticFiles();
      app.UseStaticFiles(new StaticFileOptions {
        FileProvider = new PhysicalFileProvider(STORAGE_PATH + "/files"),
        RequestPath = $"/{CONTENT_REQUEST}"
      });
      app.UseStaticFiles(new StaticFileOptions {
        FileProvider = new PhysicalFileProvider(STORAGE_PATH + "/files"),
        RequestPath = "/storage"
      });
      app.UseStaticFiles(new StaticFileOptions {
        FileProvider = new PhysicalFileProvider(STORAGE_PATH + "/thumb"),
        RequestPath = "/thumb"
      });

      app.UseStaticFiles(new StaticFileOptions {
        FileProvider = new PhysicalFileProvider(STORAGE_PATH + "/dist"),
        RequestPath = "/download/dist"
      });

      app.UseStaticFiles(new StaticFileOptions {
        ServeUnknownFileTypes = true
      });

      //app.UseSpaStaticFiles();
      app.UseRouting();

      app.UseAuthentication();
      app.UseAuthorization();

      app.UseEndpoints(endpoints => {
        endpoints.MapControllers();
      });

      //app.Run(async context =>
      //{
      //  Console.WriteLine(context.Request.Path);
      //  if (context.Request.Path == "/#/setup" || SetupModel.User != null) return;

      //  context.Response.Redirect("/#/setup");
      //});

      app.Map("/update", (_) => { });

      app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        //c.RoutePrefix = string.Empty;
      });

      //var webSocketOptions = new WebSocketOptions() {
      //  KeepAliveInterval = Timeout.InfiniteTimeSpan
      //};

      app.UseWebSockets();
      app.UseWebSocketManager("/v3/communicate");


      app.UseSpa(spa => {
        spa.Options.SourcePath = "ClientApp";
        spa.Options.DefaultPageStaticFileOptions = new StaticFileOptions {

          //OnPrepareResponse = ctx => {
          //  ctx.Context.Response.Headers.Add("Location", "/setup");
          //}
        };
        //spa.Options.DefaultPage = "/index.html";
        //  if (env.IsDevelopment()) {
        //    //spa.UseReactDevelopmentServer(npmScript: "start");
        //  }
      });
    }

    private void SetSetup(IServiceProvider serviceProvider)
    {
      var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
      SetupEntity entity = context.Setup.AsNoTracking().FirstOrDefault(s => s.Id == 1);

      entity.Server ??= new ServerJson {
        Timezone = "Europe/Moscow",
        Url = "http://*:5000"
      };
      SetupModel = entity;
    }

    private void Migrate(IServiceProvider serviceProvider)
    {
      var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
      context.Database.Migrate();
    }
  }

  public class SetupModel
  {
    public string Language { get; set; }

    public CompanyModel Company { get; set; }

    public ServerModel Server { get; set; }

    public SmtpModel Smtp { get; set; }

    public class CompanyModel
    {
      public string Name { get; set; }

      public string OwnerEmail { get; set; }

      public List<string> AccessUsers { get; set; }
    }

    public class ServerModel
    {
      public string Timezone { get; set; }

      public string Url { get; set; }

      public bool Ssl { get; set; }

      public string CertificatePath { get; set; }
    }

    public class SmtpModel
    {
      public string Url { get; set; }

      public string Login { get; set; }

      public string Password { get; set; }

      public bool Ssl { get; set; }
    }
  }

  public class SettingsModel
  {
    public string FFmpegPath { get; set; }

    public string StoragePath { get; set; }

    public string CromeDriverPath { get; set; }
  }

  public static class WebSocketManagerExtensions
  {
    public static IServiceCollection AddWebSocketServices(this IServiceCollection services)
    {
      services.AddSingleton<WebSocketService>();
      services.AddTransient<DeviceService>();

      return services;
    }

    public static IApplicationBuilder UseWebSocketManager(this IApplicationBuilder app,
                                                          PathString path)
    {
      return app.Map(path, (_app) => _app.UseMiddleware<WsDeviceMiddleware>());
    }
  }
}
