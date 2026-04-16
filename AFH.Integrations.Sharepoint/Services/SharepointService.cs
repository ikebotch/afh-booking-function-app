using AFH.Integrations.Sharepoint.Connector;
using Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Integrations.Sharepoint.Services
{
	public partial class SharepointService
	{
		private readonly SharepointConnector _sharepointConnector;
		private readonly ILogger<SharepointService> _logger;

		public SharepointService(SharepointConnector sharepointConnector, ILogger<SharepointService> logger)
		{
			_sharepointConnector = sharepointConnector;
			_logger = logger;
		}

		public async Task<List<ListItemCollectionResponse>> GetListsResponse(string siteGUID, string[] listsGUID, string[]? selectFields = null, string[]? expandFields = null, string filter = null)
		{
			try
			{
				List<ListItemCollectionResponse> data = new List<ListItemCollectionResponse>();
				foreach (var id in listsGUID)
				{
					data.Add(await _sharepointConnector.GetListData(siteGUID, id, selectFields, expandFields, filter));
				}
				return data;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retreiving sharepoints list data.");
				return null;
			}
		}

		public async Task<List<ListItem>> GetListItems(string siteGUID, string listGUID, string[]? selectFields = null, string[]? expandFields = null, string filter = null)
		{
			var data = await _sharepointConnector.GetListData(siteGUID, listGUID, selectFields, expandFields, filter);

			if (data != null)
			{
				return data.Value;
			}
			return null;
		}

		public async Task<ListItem> UpdateListItem(string siteGUID, string listGUID, string itemId, Dictionary<string, object> updateData)
		{
			var updatedItem = new ListItem
			{
				Fields = new FieldValueSet
				{
					AdditionalData = updateData
				}
			};

			var data = await _sharepointConnector.UpdateListItem(siteGUID, listGUID, itemId, updatedItem);

			return data;
		}
		public async Task<ListItem> AddListItem(string siteGUID, string listGUID, Dictionary<string, object> item)
		{
			try
			{
				var data = new ListItem
				{
					Fields = new FieldValueSet
					{
						AdditionalData = item
					}
				};
				var savedItem = await _sharepointConnector.AddListItem(siteGUID, listGUID, data);
				return savedItem;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating List Item.");
				return null;
			}
		}
		public async Task<bool> AddListItems(string siteGUID, string listGUID, IEnumerable<Dictionary<string, object>> items)
		{
			List<ListItem> data = new List<ListItem>();

			foreach (var item in items)
			{
				data.Add(new ListItem
				{
					Fields = new FieldValueSet
					{
						AdditionalData = item
					}
				});
			}

			var success = await _sharepointConnector.AddListItems(siteGUID, listGUID, data);

			return success;
		}
		/// <summary>
		/// This removes all the data in the list, use with caution
		/// </summary>
		/// <param name="siteGUID"></param>
		/// <param name="listGUID"></param>
		/// <returns>bool</returns>
		public async Task<bool> ClearListItems(string siteGUID, string listGUID)
		{
			var sucess = await _sharepointConnector.ClearListData(siteGUID, listGUID);

			return sucess;
		}
	}
}
