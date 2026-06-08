using System.IO;
using Microsoft.EntityFrameworkCore;
using rrs_pay.Models;

namespace rrs_pay.Data;

public class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<PayrollDetail> PayrollDetails => Set<PayrollDetail>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PayrollSetting> PayrollSettings => Set<PayrollSetting>();

    public static string DefaultDatabasePath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RRS Pay",
                "Data");

            return Path.Combine(folder, "rrs_pay.db");
        }
    }

    public static AppDbContext CreateDefault()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={DefaultDatabasePath}")
            .Options;

        return new AppDbContext(options);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var directory = Path.GetDirectoryName(DefaultDatabasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            optionsBuilder.UseSqlite($"Data Source={DefaultDatabasePath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsersAndRoles(modelBuilder);
        ConfigureOrganization(modelBuilder);
        ConfigureAttendance(modelBuilder);
        ConfigurePayroll(modelBuilder);
        ConfigureAuditAndSettings(modelBuilder);
        SeedData(modelBuilder);
    }

    private static void ConfigureUsersAndRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(role => role.Name).HasMaxLength(60).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(250);
            entity.HasIndex(role => role.Name).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Username).HasMaxLength(80).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(128).IsRequired();
            entity.Property(user => user.FullName).HasMaxLength(120).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(150);
            entity.HasIndex(user => user.Username).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique().HasFilter("Email IS NOT NULL");

            entity.HasOne(user => user.Role)
                .WithMany(role => role.Users)
                .HasForeignKey(user => user.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(user => user.Employee)
                .WithOne()
                .HasForeignKey<User>(user => user.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureOrganization(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.Property(department => department.Code).HasMaxLength(20).IsRequired();
            entity.Property(department => department.Name).HasMaxLength(120).IsRequired();
            entity.Property(department => department.Description).HasMaxLength(250);
            entity.HasIndex(department => department.Code).IsUnique();
            entity.HasIndex(department => department.Name).IsUnique();
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.Property(position => position.Code).HasMaxLength(30).IsRequired();
            entity.Property(position => position.Title).HasMaxLength(120).IsRequired();
            entity.Property(position => position.Description).HasMaxLength(250);
            entity.Property(position => position.DefaultBasicSalary).HasPrecision(18, 2);
            entity.HasIndex(position => position.Code).IsUnique();

            entity.HasOne(position => position.Department)
                .WithMany(department => department.Positions)
                .HasForeignKey(position => position.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.Property(employee => employee.EmployeeNumber).HasMaxLength(30).IsRequired();
            entity.Property(employee => employee.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(employee => employee.LastName).HasMaxLength(80).IsRequired();
            entity.Property(employee => employee.Email).HasMaxLength(150);
            entity.Property(employee => employee.Phone).HasMaxLength(50);
            entity.Property(employee => employee.Address).HasMaxLength(250);
            entity.Property(employee => employee.BasicSalary).HasPrecision(18, 2);
            entity.Property(employee => employee.DefaultAllowance).HasPrecision(18, 2);
            entity.Property(employee => employee.DefaultTaxRate).HasPrecision(5, 4);
            entity.Property(employee => employee.BankName).HasMaxLength(120);
            entity.Property(employee => employee.BankAccountNumber).HasMaxLength(80);
            entity.HasIndex(employee => employee.EmployeeNumber).IsUnique();
            entity.HasIndex(employee => employee.Email).IsUnique().HasFilter("Email IS NOT NULL");

            entity.HasOne(employee => employee.Department)
                .WithMany(department => department.Employees)
                .HasForeignKey(employee => employee.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(employee => employee.Position)
                .WithMany(position => position.Employees)
                .HasForeignKey(employee => employee.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAttendance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.Property(attendance => attendance.HoursWorked).HasPrecision(6, 2);
            entity.Property(attendance => attendance.OvertimeHours).HasPrecision(6, 2);
            entity.Property(attendance => attendance.Notes).HasMaxLength(250);
            entity.HasIndex(attendance => new { attendance.EmployeeId, attendance.Date }).IsUnique();

            entity.HasOne(attendance => attendance.Employee)
                .WithMany(employee => employee.Attendances)
                .HasForeignKey(attendance => attendance.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePayroll(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payroll>(entity =>
        {
            entity.Property(payroll => payroll.PayrollNumber).HasMaxLength(40).IsRequired();
            entity.Property(payroll => payroll.Notes).HasMaxLength(500);
            entity.Property(payroll => payroll.GrossTotal).HasPrecision(18, 2);
            entity.Property(payroll => payroll.DeductionTotal).HasPrecision(18, 2);
            entity.Property(payroll => payroll.TaxTotal).HasPrecision(18, 2);
            entity.Property(payroll => payroll.NetTotal).HasPrecision(18, 2);
            entity.HasIndex(payroll => payroll.PayrollNumber).IsUnique();

            entity.HasOne(payroll => payroll.CreatedByUser)
                .WithMany(user => user.CreatedPayrolls)
                .HasForeignKey(payroll => payroll.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(payroll => payroll.ApprovedByUser)
                .WithMany(user => user.ApprovedPayrolls)
                .HasForeignKey(payroll => payroll.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PayrollDetail>(entity =>
        {
            entity.Property(detail => detail.BasicSalary).HasPrecision(18, 2);
            entity.Property(detail => detail.Allowance).HasPrecision(18, 2);
            entity.Property(detail => detail.Bonus).HasPrecision(18, 2);
            entity.Property(detail => detail.OvertimePay).HasPrecision(18, 2);
            entity.Property(detail => detail.Deduction).HasPrecision(18, 2);
            entity.Property(detail => detail.Tax).HasPrecision(18, 2);
            entity.Property(detail => detail.GrossPay).HasPrecision(18, 2);
            entity.Property(detail => detail.NetPay).HasPrecision(18, 2);
            entity.Property(detail => detail.Notes).HasMaxLength(500);
            entity.HasIndex(detail => new { detail.PayrollId, detail.EmployeeId }).IsUnique();

            entity.HasOne(detail => detail.Payroll)
                .WithMany(payroll => payroll.Details)
                .HasForeignKey(detail => detail.PayrollId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(detail => detail.Employee)
                .WithMany(employee => employee.PayrollDetails)
                .HasForeignKey(detail => detail.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditAndSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(audit => audit.Action).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.EntityName).HasMaxLength(120).IsRequired();
            entity.Property(audit => audit.EntityId).HasMaxLength(80);
            entity.Property(audit => audit.Details).HasMaxLength(1000);
            entity.HasIndex(audit => audit.Timestamp);

            entity.HasOne(audit => audit.User)
                .WithMany(user => user.AuditLogs)
                .HasForeignKey(audit => audit.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PayrollSetting>(entity =>
        {
            entity.Property(setting => setting.Key).HasMaxLength(80).IsRequired();
            entity.Property(setting => setting.Value).HasMaxLength(250).IsRequired();
            entity.Property(setting => setting.Description).HasMaxLength(500);
            entity.HasIndex(setting => setting.Key).IsUnique();
        });
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        const string adminPasswordHash = "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9";

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin", Description = "Full system administration access.", IsSystemRole = true },
            new Role { Id = 2, Name = "Payroll Manager", Description = "Can manage payroll runs and approvals.", IsSystemRole = true },
            new Role { Id = 3, Name = "HR Officer", Description = "Can manage employee and attendance records.", IsSystemRole = true },
            new Role { Id = 4, Name = "Employee", Description = "Employee self-service access placeholder.", IsSystemRole = true });

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = adminPasswordHash,
                FullName = "System Administrator",
                Email = "admin@rrspay.local",
                RoleId = 1,
                IsActive = true,
                CreatedAt = seedDate
            },
            new User
            {
                Id = 2,
                Username = "payroll.manager",
                PasswordHash = adminPasswordHash,
                FullName = "Payroll Manager",
                Email = "payroll.manager@rrspay.local",
                RoleId = 2,
                IsActive = true,
                CreatedAt = seedDate
            },
            new User
            {
                Id = 3,
                Username = "hr.officer",
                PasswordHash = adminPasswordHash,
                FullName = "HR Officer",
                Email = "hr.officer@rrspay.local",
                RoleId = 3,
                IsActive = true,
                CreatedAt = seedDate
            });

        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Code = "HR", Name = "Human Resources", Description = "People operations and compliance.", IsActive = true },
            new Department { Id = 2, Code = "FIN", Name = "Finance", Description = "Accounting and payroll operations.", IsActive = true },
            new Department { Id = 3, Code = "OPS", Name = "Operations", Description = "Daily business operations.", IsActive = true },
            new Department { Id = 4, Code = "IT", Name = "Information Technology", Description = "Systems and support.", IsActive = true });

        modelBuilder.Entity<Position>().HasData(
            new Position { Id = 1, Code = "HR-MGR", Title = "HR Manager", DepartmentId = 1, DefaultBasicSalary = 72000m, Description = "Leads HR operations.", IsActive = true },
            new Position { Id = 2, Code = "FIN-ACC", Title = "Accountant", DepartmentId = 2, DefaultBasicSalary = 60000m, Description = "Maintains accounts and reconciliations.", IsActive = true },
            new Position { Id = 3, Code = "FIN-PAY", Title = "Payroll Specialist", DepartmentId = 2, DefaultBasicSalary = 58000m, Description = "Processes payroll cycles.", IsActive = true },
            new Position { Id = 4, Code = "OPS-SUP", Title = "Operations Supervisor", DepartmentId = 3, DefaultBasicSalary = 54000m, Description = "Supervises operational teams.", IsActive = true },
            new Position { Id = 5, Code = "IT-SUP", Title = "Software Support Analyst", DepartmentId = 4, DefaultBasicSalary = 62000m, Description = "Supports business software.", IsActive = true });

        modelBuilder.Entity<Employee>().HasData(
            new Employee
            {
                Id = 1,
                EmployeeNumber = "EMP-0001",
                FirstName = "Aisha",
                LastName = "Rahman",
                Email = "aisha.rahman@rrspay.local",
                Phone = "+1-555-0101",
                HireDate = new DateTime(2023, 2, 1),
                Status = EmploymentStatus.Active,
                IsActive = true,
                BasicSalary = 72000m,
                DefaultAllowance = 650m,
                DefaultTaxRate = 0.10m,
                BankName = "RRS Bank",
                BankAccountNumber = "100000001",
                DepartmentId = 1,
                PositionId = 1
            },
            new Employee
            {
                Id = 2,
                EmployeeNumber = "EMP-0002",
                FirstName = "Daniel",
                LastName = "Chen",
                Email = "daniel.chen@rrspay.local",
                Phone = "+1-555-0102",
                HireDate = new DateTime(2022, 6, 15),
                Status = EmploymentStatus.Active,
                IsActive = true,
                BasicSalary = 60000m,
                DefaultAllowance = 500m,
                DefaultTaxRate = 0.10m,
                BankName = "RRS Bank",
                BankAccountNumber = "100000002",
                DepartmentId = 2,
                PositionId = 2
            },
            new Employee
            {
                Id = 3,
                EmployeeNumber = "EMP-0003",
                FirstName = "Maria",
                LastName = "Santos",
                Email = "maria.santos@rrspay.local",
                Phone = "+1-555-0103",
                HireDate = new DateTime(2024, 1, 10),
                Status = EmploymentStatus.Probation,
                IsActive = true,
                BasicSalary = 58000m,
                DefaultAllowance = 450m,
                DefaultTaxRate = 0.09m,
                BankName = "RRS Bank",
                BankAccountNumber = "100000003",
                DepartmentId = 2,
                PositionId = 3
            },
            new Employee
            {
                Id = 4,
                EmployeeNumber = "EMP-0004",
                FirstName = "Omar",
                LastName = "Khan",
                Email = "omar.khan@rrspay.local",
                Phone = "+1-555-0104",
                HireDate = new DateTime(2021, 11, 5),
                Status = EmploymentStatus.Active,
                IsActive = true,
                BasicSalary = 54000m,
                DefaultAllowance = 400m,
                DefaultTaxRate = 0.08m,
                BankName = "RRS Bank",
                BankAccountNumber = "100000004",
                DepartmentId = 3,
                PositionId = 4
            },
            new Employee
            {
                Id = 5,
                EmployeeNumber = "EMP-0005",
                FirstName = "Nina",
                LastName = "Patel",
                Email = "nina.patel@rrspay.local",
                Phone = "+1-555-0105",
                HireDate = new DateTime(2023, 9, 18),
                Status = EmploymentStatus.Active,
                IsActive = true,
                BasicSalary = 62000m,
                DefaultAllowance = 525m,
                DefaultTaxRate = 0.10m,
                BankName = "RRS Bank",
                BankAccountNumber = "100000005",
                DepartmentId = 4,
                PositionId = 5
            });

        modelBuilder.Entity<PayrollSetting>().HasData(
            new PayrollSetting { Id = 1, Key = "Currency", Value = "USD", Description = "Default payroll currency for MVP displays.", EffectiveDate = seedDate, IsActive = true },
            new PayrollSetting { Id = 2, Key = "OvertimeMultiplier", Value = "1.5", Description = "Multiplier applied to hourly overtime rate.", EffectiveDate = seedDate, IsActive = true },
            new PayrollSetting { Id = 3, Key = "DefaultTaxRate", Value = "0.10", Description = "Fallback tax rate when employee tax rate is not set.", EffectiveDate = seedDate, IsActive = true },
            new PayrollSetting { Id = 4, Key = "StandardWorkingHoursPerDay", Value = "8", Description = "Standard workday length for attendance calculations.", EffectiveDate = seedDate, IsActive = true },
            new PayrollSetting { Id = 5, Key = "StandardWorkingDaysPerMonth", Value = "22", Description = "Default working days used to derive hourly rates.", EffectiveDate = seedDate, IsActive = true },
            new PayrollSetting { Id = 6, Key = "PayrollNumberPrefix", Value = "PAY", Description = "Prefix used for generated payroll numbers.", EffectiveDate = seedDate, IsActive = true });
    }
}
