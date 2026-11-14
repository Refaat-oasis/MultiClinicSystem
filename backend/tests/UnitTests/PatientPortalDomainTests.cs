using System.Reflection;
using Core.Application;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class PatientPortalDomainTests
{
    [Fact]
    public void Given_PatientAccount_When_PrintedPinFailsMaxAttemptsOrExpires_Then_LocksAndRejectsFurtherUse()
    {
        var now = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var account = new PatientAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, false, null, now);

        string pin = PatientAccount.GeneratePrintedActivationPin();
        pin.Should().HaveLength(10);

        account.IssuePrintedActivationCode("hmac-pin-1", now.AddHours(2), now);
        account.CanAttemptPrintedPin(now.AddHours(1)).Should().BeTrue();
        account.CanAttemptPrintedPin(now.AddHours(3)).Should().BeFalse("expired PIN must be rejected");

        for (int i = 0; i < 5; i++)
        {
            account.RecordPrintedPinFailure(now.AddMinutes(i + 1));
        }

        account.PrintedCodeAttempts.Should().Be(5);
        account.PrintedCodeHmac.Should().BeNull();
        account.CanAttemptPrintedPin(now.AddMinutes(10)).Should().BeFalse("attempt limit reached");

        account.IssuePrintedActivationCode("hmac-pin-2", now.AddHours(2), now.AddMinutes(15));
        string initialStamp = account.SecurityStamp;
        account.ConsumePrintedPinAndSetPendingEmail("patient@clinic.local", now.AddMinutes(16));
        account.Status.Should().Be(PatientAccountStatus.PendingEmailConfirmation);
        account.PrintedCodeHmac.Should().BeNull("PIN is single-use");
        account.SecurityStamp.Should().NotBe(initialStamp);

        account.ActivateWithPassword("HASHED_PW", now.AddMinutes(18));
        account.Status.Should().Be(PatientAccountStatus.Active);
        account.IsEmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public void Given_PrivateDoctorNoteOrAttachmentOrVisit_When_SettingPortalVisibility_Then_PrivateNoteNeverExposesToPatient()
    {
        var now = DateTimeOffset.UtcNow;
        var privateNote = new PatientNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Private observation", true, Guid.NewGuid(), now);
        privateNote.SetVisibleToPatient(true);
        privateNote.VisibleToPatient.Should().BeFalse("doctor-private notes can never become visible on portal");

        var publicNote = new PatientNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Follow-up advice", false, Guid.NewGuid(), now);
        publicNote.VisibleToPatient.Should().BeFalse();
        publicNote.SetVisibleToPatient(true);
        publicNote.VisibleToPatient.Should().BeTrue();

        var attachment = new PatientAttachment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lab", "cbc.pdf", "application/pdf", 1024, "abc", "k1", null, null, Guid.NewGuid(), now);
        attachment.SharedWithPatient.Should().BeFalse();
        attachment.SetSharedWithPatient(true);
        attachment.SharedWithPatient.Should().BeTrue();

        var visit = new Visit(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "dental", null, 30, now, true, null, now);
        visit.SetVisibleToPatient(false, now.AddMinutes(1));
        visit.IsVisibleToPatient.Should().BeFalse();
    }

    [Fact]
    public void Given_PortalSignedToken_When_ValidatedWithinExpiryForSamePatientAndResource_Then_SucceedsAndRejectsOthersOrExpired()
    {
        string secret = "portal-signing-secret-test-32-chars-minimum";
        Guid clinicId = Guid.NewGuid();
        Guid patientId = Guid.NewGuid();
        Guid resourceId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        long exp = now.AddMinutes(5).ToUnixTimeSeconds();

        string token = PortalSignedTokenHelper.GenerateToken(secret, clinicId, patientId, "prescription", resourceId, exp);

        PortalSignedTokenHelper.ValidateToken(secret, token, clinicId, patientId, "prescription", resourceId, now.AddMinutes(2))
            .Should().BeTrue();

        PortalSignedTokenHelper.ValidateToken(secret, token, clinicId, Guid.NewGuid(), "prescription", resourceId, now.AddMinutes(2))
            .Should().BeFalse("token must not validate for another patient");

        PortalSignedTokenHelper.ValidateToken(secret, token, clinicId, patientId, "attachment", resourceId, now.AddMinutes(2))
            .Should().BeFalse("token must not validate for another resource kind");

        PortalSignedTokenHelper.ValidateToken(secret, token, clinicId, patientId, "prescription", resourceId, now.AddMinutes(6))
            .Should().BeFalse("expired token must be rejected");
    }

    [Fact]
    public void Given_PortalApplicationDtos_When_InspectingProperties_Then_NoCostMarkupLabOrPrivateNoteFieldsExist()
    {
        string[] forbiddenTokens = ["ToolCost", "MaterialsCost", "Markup", "LabCost", "PrivateNote", "PrivateDoctorNotes", "InternalNote"];

        Type[] portalTypes = typeof(PortalVisitSummaryResult).Assembly
            .GetTypes()
            .Where(t => t.Name.StartsWith("Portal", StringComparison.Ordinal))
            .ToArray();

        portalTypes.Should().NotBeEmpty();

        foreach (Type type in portalTypes)
        {
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (string token in forbiddenTokens)
                {
                    prop.Name.Contains(token, StringComparison.OrdinalIgnoreCase)
                        .Should().BeFalse($"portal DTO {type.Name}.{prop.Name} must never expose {token}");
                }
            }
        }
    }
}

