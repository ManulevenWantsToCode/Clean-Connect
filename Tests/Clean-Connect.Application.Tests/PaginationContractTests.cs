using System.ComponentModel.DataAnnotations;
using Clean_Connect.Application.DTO.Pagination;
using Xunit;

namespace Clean_Connect.Application.Tests;

public sealed class PaginationContractTests
{
    [Fact]
    public void Request_defaults_to_first_page_and_standard_page_size()
    {
        var request = new PaginationRequest();
        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Equal(0, request.Offset);
        Assert.Equal(100, PaginationRequest.MaximumPageSize);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Invalid_page_or_page_size_fails_validation(int page, int pageSize)
    {
        var request = new PaginationRequest { Page = page, PageSize = pageSize };
        var validationResults = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true));
    }

    [Fact]
    public void Empty_result_has_zero_total_pages_and_no_next_page()
    {
        var page = new PagedResponse<string>(Array.Empty<string>(), 1, 20, 0, 0, false, false);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }
}
