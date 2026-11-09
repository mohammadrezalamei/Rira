using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rira.Domain.Entities;
using Rira.Domain.ValueObjects;

namespace Rira.Infrastructure.Persistence.Configurations;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("Persons", table =>
        {
            table.HasCheckConstraint("CK_Persons_FirstName", "length(trim(FirstName)) BETWEEN 1 AND 100");
            table.HasCheckConstraint("CK_Persons_LastName", "length(trim(LastName)) BETWEEN 1 AND 100");
            table.HasCheckConstraint("CK_Persons_NationalCode", "length(NationalCode) = 10 AND NationalCode NOT GLOB '*[^0-9]*'");
        });
        builder.HasKey(person => person.Id);
        builder.Property(person => person.Id).ValueGeneratedNever();
        builder.Ignore(person => person.DomainEvents);
        builder.Property(person => person.FirstName).IsRequired()
            .HasMaxLength(Person.MaxFirstNameLength).IsConcurrencyToken();
        builder.Property(person => person.LastName).IsRequired()
            .HasMaxLength(Person.MaxLastNameLength).IsConcurrencyToken();
        builder.Property(person => person.NationalCode)
            .HasConversion(code => code.Value, value => ReadNationalCode(value))
            .HasMaxLength(NationalCode.RequiredLength).IsRequired().IsConcurrencyToken();
        builder.HasIndex(person => person.NationalCode).IsUnique();
        builder.Property(person => person.BirthDate)
            .HasConversion(date => date.Value, value => ReadBirthDate(value))
            .IsRequired().IsConcurrencyToken();
    }

    private static NationalCode ReadNationalCode(string value)
    {
        var result = NationalCode.Create(value);
        return result.IsSuccess ? result.Value! : throw new InvalidOperationException("Invalid stored national code.");
    }

    private static BirthDate ReadBirthDate(DateOnly value)
    {
        // Reading persisted values must not depend on the current system date.
        var result = BirthDate.Create(value, DateOnly.MaxValue);
        return result.IsSuccess ? result.Value! : throw new InvalidOperationException("Invalid stored birth date.");
    }
}
