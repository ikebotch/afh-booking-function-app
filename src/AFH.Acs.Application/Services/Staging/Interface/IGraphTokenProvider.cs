namespace AFH.Acs.Recorder.Services.Interface;

public interface IGraphTokenProvider
{
    Task<string> GetTokenAsync();
}
