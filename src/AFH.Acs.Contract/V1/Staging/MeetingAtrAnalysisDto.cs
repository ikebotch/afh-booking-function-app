namespace AFH.Acs.Recorder.DTOs;

public class MeetingAtrAnalysisDto
{
    /// <summary>
    /// MeetingId for which this ATR analysis was produced.
    /// </summary>
    public string MeetingId { get; set; } = default!;

    /// <summary>
    /// Adviser’s or system-generated extracted attitude to risk text.
    /// Typically taken from meeting notes or CRM.
    /// </summary>
    public string ClientAtrText { get; set; } = default!;

    /// <summary>
    /// List of matched ATR paragraphs from the AFH risk library.
    /// </summary>
    public List<AtrMatchedParagraphDto> MatchedParagraphs { get; set; } = new();

    /// <summary>
    /// Key points the client covered explicitly.
    /// </summary>
    public List<string> HighlightedKeypoints { get; set; } = new();

    /// <summary>
    /// Key points the client did NOT cover (gaps).
    /// Useful for compliance and adviser review.
    /// </summary>
    public List<string> MissingKeypoints { get; set; } = new();

    /// <summary>
    /// System-generated summary of the ATR interpretation.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// AI or rule-based assessment of consistency with recommended risk level.
    /// </summary>
    public string? RiskAlignment { get; set; }

    /// <summary>
    /// Any warnings or anomalies detected (no ATR provided, conflicting statements, etc.)
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

public class AtrMatchedParagraphDto
{
    /// <summary>
    /// Paragraph ID in the AFH ATR knowledge base.
    /// </summary>
    public string ParagraphId { get; set; } = default!;

    /// <summary>
    /// Paragraph heading / key point (“Capacity for Loss”, “Long-term perspective”, etc.)
    /// </summary>
    public string Header { get; set; } = default!;

    /// <summary>
    /// Full text of the matched paragraph.
    /// </summary>
    public string Text { get; set; } = default!;

    /// <summary>
    /// Score (e.g., relevance, confidence).
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Extracted client sentences that caused this match.
    /// </summary>
    public List<string> Evidence { get; set; } = new();
}