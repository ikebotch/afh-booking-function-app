namespace AFH.Acs.Recorder.DTOs;
public class ClientOverviewSPDto
{
    public string ODataEtag { get; set; }
    public string ClientId { get; set; }
    public string LinkTitle { get; set; }
    public string LastMeetingSummary { get; set; }
    public string ObjectivesAndGoals { get; set; }
    public string RelationshipBuildingFacts { get; set; }
    public string RiskAndVulnerability { get; set; }
    public string ContentType { get; set; }
    public DateTime? Modified { get; set; }
    public DateTime? MeetingDate { get; set; }
    public int? AuthorLookupId { get; set; }
    public int? EditorLookupId { get; set; }
    public string UIVersionString { get; set; }
    public bool? Attachments { get; set; }
    public string Edit { get; set; }
    public string LinkTitleNoMenu { get; set; }
    public int? ItemChildCount { get; set; }
    public int? FolderChildCount { get; set; }
    public string ComplianceFlags { get; set; }
    public string ComplianceTag { get; set; }
    public DateTime? ComplianceTagWrittenTime { get; set; }
    public int? ComplianceTagUserId { get; set; }
    public int? AppAuthorLookupId { get; set; }
    public int? AppEditorLookupId { get; set; }
}