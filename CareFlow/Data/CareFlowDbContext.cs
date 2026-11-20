using System;
using CareFlow.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Data;

public class CareFlowDbContext : DbContext
{
    public CareFlowDbContext(DbContextOptions<CareFlowDbContext> options) : base(options)
    {
    }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<DiagnosticService> DiagnosticServices => Set<DiagnosticService>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Patient Configuration
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("Patients");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContactNumber).HasMaxLength(20);
            entity.Property(e => e.DateOfBirth).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        // DiagnosticService Configuration
        modelBuilder.Entity<DiagnosticService>(entity =>
        {
            entity.ToTable("DiagnosticServices");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.DailyCapacity).IsRequired();

            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Booking Configuration
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("Bookings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReferenceNumber).IsRequired().HasMaxLength(30);
            entity.Property(e => e.BookingDate).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);

            entity.HasIndex(e => e.ReferenceNumber).IsUnique();
            entity.HasIndex(e => e.BookingDate);

            entity.HasOne(e => e.Patient)
                .WithMany(p => p.Bookings)
                .HasForeignKey(e => e.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // BookingItem Configuration
        modelBuilder.Entity<BookingItem>(entity =>
        {
            entity.ToTable("BookingItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).IsRequired();

            // A service cannot be added twice to the same booking (BR-06 relational constraint)
            entity.HasIndex(e => new { e.BookingId, e.DiagnosticServiceId }).IsUnique();

            entity.HasOne(e => e.Booking)
                .WithMany(b => b.Items)
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DiagnosticService)
                .WithMany(s => s.BookingItems)
                .HasForeignKey(e => e.DiagnosticServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed initial data
        SeedInitialData(modelBuilder);
    }

    private static void SeedInitialData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Patient>().HasData(
            new Patient { Id = 1, FullName = "Juan Dela Cruz", DateOfBirth = new DateTime(1985, 3, 15), ContactNumber = "0917-123-4567", CreatedAt = new DateTime(2026, 1, 1) },
            new Patient { Id = 2, FullName = "Maria Clara Santos", DateOfBirth = new DateTime(1992, 7, 24), ContactNumber = "0918-234-5678", CreatedAt = new DateTime(2026, 1, 1) },
            new Patient { Id = 3, FullName = "Jose Rizal", DateOfBirth = new DateTime(1978, 6, 19), ContactNumber = "0919-345-6789", CreatedAt = new DateTime(2026, 1, 1) },
            new Patient { Id = 4, FullName = "Andres Bonifacio", DateOfBirth = new DateTime(1990, 11, 30), ContactNumber = "0920-456-7890", CreatedAt = new DateTime(2026, 1, 1) },
            new Patient { Id = 5, FullName = "Gabriela Silang", DateOfBirth = new DateTime(1995, 3, 19), ContactNumber = "0921-567-8901", CreatedAt = new DateTime(2026, 1, 1) }
        );

        modelBuilder.Entity<DiagnosticService>().HasData(
            new DiagnosticService { Id = 1, Code = "CBC", Name = "Complete Blood Count", Description = "Evaluates overall health and detects wide range of disorders.", DailyCapacity = 5 },
            new DiagnosticService { Id = 2, Code = "URN", Name = "Urinalysis", Description = "Detects and manages a wide range of disorders, such as urinary tract infections.", DailyCapacity = 4 },
            new DiagnosticService { Id = 3, Code = "CXR", Name = "Chest X-Ray", Description = "Produces images of heart, lungs, blood vessels, airways, and bones.", DailyCapacity = 3 },
            new DiagnosticService { Id = 4, Code = "ECG", Name = "Electrocardiogram", Description = "Records electrical signals from heart to check for heart conditions.", DailyCapacity = 2 },
            new DiagnosticService { Id = 5, Code = "LIP", Name = "Lipid Panel", Description = "Measures cholesterol and triglyceride levels in your blood.", DailyCapacity = 4 },
            new DiagnosticService { Id = 6, Code = "FBS", Name = "Fasting Blood Sugar", Description = "Measures blood glucose after an overnight fast to screen for diabetes.", DailyCapacity = 5 }
        );
    }
}
