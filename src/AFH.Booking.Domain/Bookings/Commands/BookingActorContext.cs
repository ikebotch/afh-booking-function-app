namespace AFH.Booking.Domain.Bookings.Commands;

public sealed record BookingActorContext(
    string SourceApplication,
    string ActorType,
    string? ActorId,
    string? DisplayName,
    string? CorrelationId,
    bool IsSelfService,
    bool CanOverrideRules,
    IReadOnlySet<string> Permissions)
{
    public const string SourceSelfService = "SelfService";
    public const string SourceLeadTech = "LeadTech";
    public const string SourceInternalAdmin = "InternalAdmin";
    public const string SourceApprovalWorkflow = "ApprovalWorkflow";
    public const string SourceSystemJob = "SystemJob";

    public const string ActorClient = "Client";
    public const string ActorLeadTech = "LeadTech";
    public const string ActorInternalAdmin = "InternalAdmin";
    public const string ActorAdviser = "Adviser";
    public const string ActorSystem = "System";

    public static BookingActorContext SelfServiceClient(
        string? actorId,
        string? correlationId = null)
        => Create(
            SourceSelfService,
            ActorClient,
            actorId,
            displayName: null,
            correlationId,
            isSelfService: true,
            canOverrideRules: false,
            permissions: null);

    public static BookingActorContext LeadTech(
        string? actorId = null,
        string? displayName = null,
        string? correlationId = null,
        IEnumerable<string>? permissions = null)
        => Create(
            SourceLeadTech,
            ActorLeadTech,
            actorId,
            displayName,
            correlationId,
            isSelfService: false,
            canOverrideRules: false,
            permissions);

    public static BookingActorContext InternalAdmin(
        string? actorId = null,
        string? displayName = null,
        string? correlationId = null,
        bool canOverrideRules = false,
        IEnumerable<string>? permissions = null,
        string actorType = ActorInternalAdmin,
        string sourceApplication = SourceInternalAdmin)
        => Create(
            sourceApplication,
            actorType,
            actorId,
            displayName,
            correlationId,
            isSelfService: false,
            canOverrideRules,
            permissions);

    public static BookingActorContext ApprovalWorkflow(
        string? actorId,
        string? displayName = null,
        string? correlationId = null,
        IEnumerable<string>? permissions = null)
        => Create(
            SourceApprovalWorkflow,
            ActorAdviser,
            actorId,
            displayName,
            correlationId,
            isSelfService: false,
            canOverrideRules: false,
            permissions);

    public static BookingActorContext SystemJob(
        string? actorId,
        string? correlationId = null,
        string sourceApplication = SourceSystemJob)
        => Create(
            sourceApplication,
            ActorSystem,
            actorId,
            displayName: actorId,
            correlationId,
            isSelfService: false,
            canOverrideRules: true,
            permissions: null);

    private static BookingActorContext Create(
        string sourceApplication,
        string actorType,
        string? actorId,
        string? displayName,
        string? correlationId,
        bool isSelfService,
        bool canOverrideRules,
        IEnumerable<string>? permissions)
        => new(
            NormalizeRequired(sourceApplication, nameof(sourceApplication)),
            NormalizeRequired(actorType, nameof(actorType)),
            NormalizeOptional(actorId),
            NormalizeOptional(displayName),
            NormalizeOptional(correlationId),
            isSelfService,
            canOverrideRules,
            NormalizePermissions(permissions));

    private static string NormalizeRequired(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} is required.", name);

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlySet<string> NormalizePermissions(IEnumerable<string>? permissions)
        => permissions is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : permissions
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Select(permission => permission.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
