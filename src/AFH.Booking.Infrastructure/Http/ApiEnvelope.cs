using System.Text.Json.Serialization;

namespace AFH.Booking.Infrastructure.Http;

public sealed class ApiEnvelope<T> where T : class
{
    public bool Success { get; set; }
    public T? Data { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiPaging? Paging { get; set; }
}

public sealed class ApiPaging
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
