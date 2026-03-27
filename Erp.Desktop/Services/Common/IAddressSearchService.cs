namespace Erp.Desktop.Services;

public interface IAddressSearchService
{
    AddressSearchResult? Search(string? keyword);
}

public sealed record AddressSearchResult(string ZipNo, string RoadAddress, string JibunAddress);
