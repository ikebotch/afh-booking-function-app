using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace AFH.Integrations.Sharepoint.Connector
{

	public partial class SharepointConnector
	{
		private readonly GraphServiceClient _graphClient;
		private readonly ILogger<SharepointConnector> _logger;
		public SharepointConnector(GraphServiceClient graphClient, ILogger<SharepointConnector> logger)
		{
			_graphClient = graphClient;
			_logger = logger;
		}


		public async Task<ListItemCollectionResponse> GetListData(string siteGUID, string listGUID, string[]? selectFields = null, string[]? expandFields = null, string filter = null)
		{
			try
			{

				var data = await _graphClient.Sites[siteGUID].Lists[listGUID].Items.GetAsync(requestConfiguration =>
				{

					if (expandFields != null && expandFields.Any())
					{
						requestConfiguration.QueryParameters.Expand =
							expandFields;
						

					}
					if(filter != null)
					{
						//IMPORTANT: Make sure field you filtering is indexed in sharepoint list:example "fields/ClientEntityID eq 333 or fields/ClientEntityID eq 33579";
						requestConfiguration.QueryParameters.Filter = filter;
					}
					if (selectFields != null && selectFields.Any())
					{
						//TODO Need to find a way of how to select only few fi elds from custom fields, as this seems to only work with high level property e.g. ID
						requestConfiguration.QueryParameters.Select =
							selectFields;
					}
				});

				//response.Value = new List<ListItem>();
				ListItemCollectionResponse response = new ListItemCollectionResponse();
				response.Value = new List<ListItem>();
				response.AdditionalData = data.AdditionalData;
				var pageIterator = PageIterator<ListItem, ListItemCollectionResponse>.CreatePageIterator(_graphClient, data, item =>
				{
					response.Value.Add(item);
					return true;
				});

				await pageIterator.IterateAsync();

				return response;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retreiving sharepoints collection response.");
				return null;
			}
		}

		public async Task<ListItem> UpdateListItem(string siteGUID, string listGUID, string itemId, ListItem item)
		{
			try
			{
				var results = await _graphClient.Sites[siteGUID].Lists[listGUID].Items[itemId].PatchAsync(item);

				return results;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating List Item.");
				return null;
			}
		}


		public async Task<ListItem> AddListItem(string siteGUID, string listGUID, ListItem item)
		{
			try
			{
				var results = await _graphClient.Sites[siteGUID].Lists[listGUID].Items.PostAsync(item);

				return results;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating List Item.");
				return null;
			}
		}

		public async Task<bool> AddListItems(string siteGUID, string listGUID, IEnumerable<ListItem> items)
		{
			try
			{
				if (items != null && items.Any())
				{
					var batchRequestContent = new BatchRequestContentCollection(_graphClient);
					foreach (var item in items)
					{
						await batchRequestContent.AddBatchRequestStepAsync(_graphClient.Sites[siteGUID].Lists[listGUID].Items.ToPostRequestInformation(item));

					}
					var returnedResponse = await _graphClient.Batch.PostAsync(batchRequestContent);
					return true;
				}
				return false;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating List Item.");
				return false;
			}
		}


		public async Task<bool> ClearListData(string siteGUID, string listGUID)
		{
			try
			{
				var listItems = await GetListData(siteGUID, listGUID, selectFields: new string[] { "Id" });

				if (listItems != null && listItems.Value != null && listItems.Value.Any())
				{
					var batchRequestContent = new BatchRequestContentCollection(_graphClient);

					foreach (var item in listItems.Value)
					{
						await batchRequestContent.AddBatchRequestStepAsync(_graphClient.Sites[siteGUID].Lists[listGUID].Items[item.Id].ToDeleteRequestInformation());
					}

					var returnedResponse = await _graphClient.Batch.PostAsync(batchRequestContent);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error clearing data from the list.");
				return false;
			}
			return true;
		}

		public async Task<bool> ListExists(string siteGuid,string listGuid)
        {
            try
            {
                var list = await _graphClient.Sites[siteGuid].Lists[listGuid].GetAsync();
                return list != null;
            }
            catch (ServiceException ex)
            {
               return false;
            }
        }
    }
}
