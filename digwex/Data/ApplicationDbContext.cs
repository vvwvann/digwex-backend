using Digwex.Models;
using IdentityServer4.EntityFramework.Options;
using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Digwex.Controllers.Api;

namespace Digwex.Data
{
  public class ApplicationDbContext : ApiAuthorizationDbContext<ApplicationUser>
  {
    public ApplicationDbContext(
        DbContextOptions options,
        IOptions<OperationalStoreOptions> operationalStoreOptions) : base(options, operationalStoreOptions)
    {

    }

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //       => optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=Digwex;Username=postgres;Password=anyany");

    protected override void OnModelCreating(ModelBuilder builder)
    {
      base.OnModelCreating(builder);

      builder.Entity<SetupEntity>().HasData(
        new SetupEntity {
          Id = 1,
          Language = "RU"
        });

      builder.Entity<PlaylistContentEntity>()
        .HasKey(s => new { s.ContentId, s.PlaylistId, s.Index });

      // CalendarPlaylistEntity

      //builder.Entity<CalendarPlaylistEntity>()
      //  .HasKey(s => new { s.CalendarId, s.PlaylistId, s.Guid });

      //builder.Entity<ApplicationUser>()
      //  .HasOne(s=>s.Settings)
      //  .WithOne(s=>s.User)
 

      builder.Entity<CalendarPlaylistEntity>()
        .HasOne(s => s.Calendar)
        .WithMany(c => c.CalendarPlaylist)
        .OnDelete(DeleteBehavior.Restrict);

      builder.Entity<CalendarPlaylistEntity>()
        .HasOne(s => s.Playlist)
        .WithMany(c => c.CalendarPlaylist)
        .OnDelete(DeleteBehavior.Restrict);

      // -----------------

      // PlaylistContentEntity

      builder.Entity<PlaylistContentEntity>()
        .HasIndex(s => new { s.PlaylistId, s.Index })
        .IsUnique();


      builder.Entity<PlaylistContentEntity>()
        .HasOne(s => s.Playlist)
        .WithMany(c => c.PlaylistContents)
        .OnDelete(DeleteBehavior.Restrict);

      builder.Entity<PlaylistContentEntity>()
        .HasOne(s => s.Content)
        .WithMany(c => c.PlaylistContents)
        .OnDelete(DeleteBehavior.Restrict);

      // ------------------

      builder.Entity<PlayerEntity>()
        .HasOne(s => s.Calendar)
        .WithMany(c => c.Players)
        .OnDelete(DeleteBehavior.SetNull);

      builder.Entity<PlayerEntity>()
        .HasIndex(s => s.PinPriv)
        .IsUnique();
    }

    public DbSet<PlaylistEntity> Playlists { get; set; }

    public DbSet<CalendarEntity> Calendars { get; set; }

    public DbSet<ContentEntity> Contents { get; set; }

    public DbSet<PlayerEntity> Players { get; set; }

    public DbSet<PlaylistContentEntity> PlaylistContent { get; set; }

    public DbSet<CalendarPlaylistEntity> CalendarPlaylist { get; set; }

    public DbSet<CommandEntity> Commands { get; set; }

    public DbSet<SettingsEntity> Settings { get; set; }

    public DbSet<SetupEntity> Setup { get; set; }
  }

  public class CalendarEntity
  {
    [Key]
    public int Id { get; set; }

    public string Name { get; set; }

    [JsonProperty("color")]
    public string Color { get; set; }

    public List<PlayerEntity> Players { get; set; }

    [NotMapped]
    public List<PlaylistEntity> Playlists { get; set; }

    [JsonIgnore]
    public List<CalendarPlaylistEntity> CalendarPlaylist { get; set; }

    [JsonIgnore]
    public ApplicationUser User { get; set; }
  }

  public class PlayerEntity
  {
    [Key]
    public int Id { get; set; }

    //[JsonIgnore]
    public CalendarEntity Calendar { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Address { get; set; }

    public bool Landscape { get; set; } = true;

    public double Lat { get; set; }

    public double Lon { get; set; }

    public string Platform { get; set; }

    public string Version { get; set; }

    public DateTime LastSync { get; set; }

    public bool StatusScreen { get; set; }

    [JsonIgnore]
    public DateTime LastOnline { get; set; }

    [NotMapped]
    [JsonProperty("lastOnline")]
    public DateTime? LastOnlineP { get; set; }

    [NotMapped]
    public bool Online { get; set; }

    public DateTime AddTime { get; set; } = DateTime.UtcNow;

    //[Required]
    public string Timezone { get; set; }

    public bool IsActivate { get; set; }

    public bool ProblemSync { get; set; }

    public int Data { get; set; }

    [JsonIgnore]
    public long PinPriv { get; set; }

    public string Pin { get; set; }

    [JsonIgnore]
    public string Token { get; set; }

    public int Percent { get; set; } = -1;

    public DateTime DeviceTime { get; set; }

    [JsonIgnore]
    public ApplicationUser User { get; set; }

    [JsonIgnore]
    [Column(TypeName = "jsonb")]
    public ResponseFile LastScreen { get; internal set; }

    [JsonIgnore]
    [Column(TypeName = "jsonb")]
    public ResponseFile LastLog { get; internal set; }
  }

  public class ContentEntity
  {
    [Key]
    public int Id { get; set; }

    public string Name { get; set; }

    public int Duration { get; set; }

    public long Size { get; set; }

    public string Url { get; set; }

    public string Md5 { get; set; }

    public string Type { get; set; }

    public string Ext { get; set; }

    public DateTime AddTime { get; set; } = DateTime.UtcNow;

    public string SmallThumb { get; set; }

    public string LargeThumb { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool Audio { get; set; }

    public bool Landscape { get; set; }

    [JsonIgnore]
    public ApplicationUser User { get; set; }

    [JsonIgnore]
    public List<PlaylistContentEntity> PlaylistContents { get; set; }
  }

  public class PlaylistEntity
  {
    [Key]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("color")]
    public string Color { get; set; }

    [Required]
    [JsonRequired]
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [NotMapped]
    public Guid Guid { get; set; }

    [NotMapped]
    public JToken Intervals { get; set; }

    [NotMapped]
    public bool Base { get; set; }

    public DateTime AddTime { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ApplicationUser User { get; set; }

    [JsonIgnore]
    public List<CalendarPlaylistEntity> CalendarPlaylist { get; set; }

    [JsonIgnore]
    public List<PlaylistContentEntity> PlaylistContents { get; set; }
  }

  public class PlaylistContentEntity
  {
    public int PlaylistId { get; set; }

    public int ContentId { get; set; }

    public int Index { get; set; }

    public PlaylistEntity Playlist { get; set; }

    public ContentEntity Content { get; set; }

    public int Duration { get; set; } = 10;

    [NotMapped]
    public string DurationStr { get; set; }

    [NotMapped]
    public string color { get; set; }

    [JsonIgnore]
    public ApplicationUser User { get; set; }
  }

  public class CalendarPlaylistEntity
  {
    [Key]
    public Guid Guid { get; set; }

    public int CalendarId { get; set; }

    public int PlaylistId { get; set; }

    public bool Base { get; set; }

    [JsonIgnore]
    public string IntrvalsPriv { get; set; }

    [NotMapped]
    public JToken Intervals {
      get {
        return JToken.Parse(IntrvalsPriv ?? "{}");
      }
      set {
        if (value == null) {
          IntrvalsPriv = "{}";
          return;
        }
        IntrvalsPriv = value.ToString(Formatting.None);
      }
    }

    public CalendarEntity Calendar { get; set; }

    public PlaylistEntity Playlist { get; set; }

    [JsonIgnore]
    public ApplicationUser User { get; set; }
  }

  public class CommandEntity
  {
    [Key]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("command")]
    public string Command { get; set; }

    [JsonIgnore]
    public int DeviceId { get; set; }
  }

  public class SettingsEntity
  {
    [Key]
    public int Id { get; set; }

    public string Language { get; set; }

    public bool TimeFormat { get; set; }

    public int FirstWeekDay { get; set; }

    public string UserId { get; set; }

    [JsonIgnore]
    public ApplicationUser User { get; set; }

    [JsonIgnore]
    public bool Main { get; set; }
  }

  public class SetupEntity
  {
    [Key]
    [JsonIgnore]
    public int Id { get; set; }

    public string Language { get; set; }

    [Column(TypeName = "jsonb")]
    public CompanyJson Company { get; set; }

    [Column(TypeName = "jsonb")]
    public ServerJson Server { get; set; }

    [Column(TypeName = "jsonb")]
    public SmtpJson Smtp { get; set; }

    [JsonIgnore]
    public string UserId { get; set; }

    [JsonIgnore]
    public ApplicationUser User { get; set; }
  }

  public class CompanyJson
  {
    [JsonProperty("companyName")]
    public string Name { get; set; }

    public string Email { get; set; }

    public List<string> AccessUsers { get; set; }
  }

  public class ServerJson
  {
    public string Timezone { get; set; }

    public string Url { get; set; }

    public bool Ssl { get; set; }

    public string CertificatePath { get; set; }
  }

  public class SmtpJson
  {
    public string Url { get; set; }

    public string Login { get; set; }

    public int Port { get; set; }

    public string Password { get; set; }

    public bool Ssl { get; set; }
  }
}
