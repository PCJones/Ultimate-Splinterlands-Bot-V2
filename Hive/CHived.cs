using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using Cryptography.ECDSA;
using Newtonsoft.Json.Linq;

namespace HiveAPI.CS
{
	public class CHived : CHiveAPI
	{
		#region Constants
		const string DATABASE_API = "database_api.";
		const string RC_API = "rc_api.";
		const string BLOCK_API = "block_api.";
		const string CONDENSER_API = "condenser_api.";
		const string HIVE_API = "hive.";

		const string CHAINID = "beeab0de00000000000000000000000000000000000000000000000000000000";
		#endregion

		#region Constructors
		public CHived(HttpClient oHttpClient, string strURL) : base(oHttpClient, strURL)
		{
		}
		#endregion

		#region private Methods
		public class CTransaction
		{
			public UInt16 ref_block_num;
			public UInt32 ref_block_prefix;
			public DateTime expiration;
			public Object[] operations;
			public Object[] extensions = Array.Empty<object>();
			public string[] signatures = Array.Empty<string>();
		}
		public class CtransactionData
		{
			public CTransaction tx;
			public string txid;
		}
		private CtransactionData SignTransaction(CTransaction oTransaction, String[] astrPrivateKeys)
		{
			CSerializer oSerializer = new CSerializer();
			byte[] msg = oSerializer.Serialize(oTransaction);

			using (MemoryStream oStream = new MemoryStream())
			{
				byte[] oChainID = Hex.HexToBytes(CHAINID);
				oStream.Write(oChainID, 0, oChainID.Length);
				oStream.Write(msg, 0, msg.Length);
				byte[] oDigest = Sha256Manager.GetHash(oStream.ToArray());
				foreach (string key in astrPrivateKeys)
				{
					Array.Resize(ref oTransaction.signatures, oTransaction.signatures.Length + 1);
					oTransaction.signatures[oTransaction.signatures.Length-1] = Hex.ToString(Secp256K1Manager.SignCompressedCompact(oDigest, CBase58.DecodePrivateWif(key)));
				}
			}
			return new CtransactionData { tx = oTransaction, txid = Hex.ToString(Sha256Manager.GetHash(msg)).Substring(0, 40) };
		}
		public CtransactionData CreateTransaction(Object[] aOperations, string[] astrPrivateKeys)
		{
            try
            {
				JObject oDGP = get_dynamic_global_properties();
				CTransaction oTransaction = new CTransaction
				{
					ref_block_num = Convert.ToUInt16((UInt32)oDGP["head_block_number"] & 0xFFFF),
					ref_block_prefix = BitConverter.ToUInt32(Hex.HexToBytes(oDGP["head_block_id"].ToString()), 4),
					expiration = Convert.ToDateTime(oDGP["time"]).AddSeconds(30),
					operations = aOperations
				};
				return SignTransaction(oTransaction, astrPrivateKeys);
			}
            catch (Exception ex)
            {
                if (ex.Message.Contains("Internal Error"))
                {
					return CreateTransaction(aOperations, astrPrivateKeys);
				}
                else
                {
					
                }
            }
			return null;
		}
		#endregion

		#region public Methods

		public string broadcast_transaction(Object[] operations, string[] keys)
		{
			CtransactionData oTransaction = CreateTransaction(operations, keys);

			for(int i = 0; i < oTransaction.tx.operations.Length; i++)
			{
				object op = oTransaction.tx.operations[i];
				oTransaction.tx.operations[i] = new Op { name = op.GetType().Name, payload = op };
			}
			ArrayList arrParams = new ArrayList();
			arrParams.Add(oTransaction.tx);

			call_api(CONDENSER_API + MethodBase.GetCurrentMethod().Name, arrParams);

			return oTransaction.txid;
		}

		public JObject get_info()
		{
			return call_api(HIVE_API + MethodBase.GetCurrentMethod().Name);
		}
		public JObject get_config()
		{
			return call_api(CONDENSER_API + MethodBase.GetCurrentMethod().Name);
		}
		public JObject get_dynamic_global_properties()
		{
			return call_api(DATABASE_API + MethodBase.GetCurrentMethod().Name);
		}
		public JObject get_chain_properties()
		{
			return call_api(MethodBase.GetCurrentMethod().Name);
		}
		public JObject get_current_median_history_price()
		{
			return call_api(MethodBase.GetCurrentMethod().Name);
		}
		public JObject get_feed_history()
		{
			return call_api(MethodBase.GetCurrentMethod().Name);
		}
		public JObject get_witness_schedule()
		{
			return call_api(MethodBase.GetCurrentMethod().Name);
		}
		public JObject get_hardfork_version()
		{
			return call_api(MethodBase.GetCurrentMethod().Name);
		}
		public JObject get_next_scheduled_hardfork()
		{
			return call_api(MethodBase.GetCurrentMethod().Name);
		}
		public JArray get_accounts(ArrayList arrAccounts)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(arrAccounts);
			return call_api_array(CONDENSER_API + MethodBase.GetCurrentMethod().Name, arrParams);
		}
		public JObject get_accounts(string strAccount)
		{
			ArrayList arrAccounts = new ArrayList();
			arrAccounts.Add(strAccount);
			return (JObject)get_accounts(arrAccounts).First;
		}
		public JArray lookup_account_names(ArrayList arrAccounts)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(arrAccounts);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}
		public JArray lookup_accounts(string strLowerbound, uint nLimit)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(strLowerbound);
			arrParams.Add(nLimit);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}
		public JValue get_account_count()
		{
			return call_api_value(MethodBase.GetCurrentMethod().Name);
		}
		public JArray get_owner_history(string strAccount)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(strAccount);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}
		public JObject get_recovery_request(string strAccount)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(strAccount);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}
		public JObject get_block_header(long lBlockID)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(lBlockID);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}
		public JObject get_block(long lBlockID)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(lBlockID);
			return call_api(CONDENSER_API + MethodBase.GetCurrentMethod().Name, arrParams);
		}
		public JObject get_ops_in_block(long block_num, bool only_virtual)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(block_num);
			arrParams.Add(only_virtual);
			return call_api(CONDENSER_API + MethodBase.GetCurrentMethod().Name, arrParams);
		}
		public JArray get_witnesses(ArrayList arrWitnesses)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(arrWitnesses);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}
		public JArray get_conversion_requests(string strAccount)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(strAccount);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JObject get_witness_by_account(string strAccount)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(strAccount);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JArray get_witnesses_by_vote(string strFrom, int nLimit)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(strFrom);
			arrParams.Add(nLimit);
			return call_api_array(CONDENSER_API + MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JValue get_witness_count()
		{
			return call_api_value(MethodBase.GetCurrentMethod().Name);
		}

		// if permlink Is "" then it will return all votes for author
		public JArray get_active_votes(string author, string permlink)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(author);
			arrParams.Add(permlink);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JObject get_content(string strAuthor, string strPermlink)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(strAuthor);
			arrParams.Add(strPermlink);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JArray get_content_replies(string parent, string parent_permlink)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(parent);
			arrParams.Add(parent_permlink);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JArray get_discussions_by_active(string tag, int limit, ArrayList filterTags = null, ArrayList selectAuthors = null, ArrayList selectTags = null, bool? truncateBody = null)
		{
			var dictParams = new Dictionary<string, object>
			{
				{ "tag", tag },
				{ "limit", limit }
			};

			if (filterTags != null) dictParams.Add("filter_tags", filterTags);
			if (selectAuthors != null) dictParams.Add("select_authors", selectAuthors);
			if (selectTags != null) dictParams.Add("select_tags", selectTags);
			if (truncateBody != null) dictParams.Add("truncate_body", truncateBody);

			return call_api_array(MethodBase.GetCurrentMethod().Name, new ArrayList { dictParams });
		}

		public JArray get_discussions_by_hot(string tag, int limit, ArrayList filterTags = null, ArrayList selectAuthors = null, ArrayList selectTags = null, bool? truncateBody = null)
		{
			var dictParams = new Dictionary<string, object>
			{
				{ "tag", tag },
				{ "limit", limit }
			};

			if (filterTags != null) dictParams.Add("filter_tags", filterTags);
			if (selectAuthors != null) dictParams.Add("select_authors", selectAuthors);
			if (selectTags != null) dictParams.Add("select_tags", selectTags);
			if (truncateBody != null) dictParams.Add("truncate_body", truncateBody);

			return call_api_array(MethodBase.GetCurrentMethod().Name, new ArrayList { dictParams });
		}

		public JArray get_discussions_by_trending(string tag, int limit, ArrayList filterTags = null, ArrayList selectAuthors = null, ArrayList selectTags = null, bool? truncateBody = null)
		{
			var dictParams = new Dictionary<string, object>
			{
				{ "tag", tag },
				{ "limit", limit }
			};

			if (filterTags != null) dictParams.Add("filter_tags", filterTags);
			if (selectAuthors != null) dictParams.Add("select_authors", selectAuthors);
			if (selectTags != null) dictParams.Add("select_tags", selectTags);
			if (truncateBody != null) dictParams.Add("truncate_body", truncateBody);

			return call_api_array(MethodBase.GetCurrentMethod().Name, new ArrayList { dictParams });
		}

		//vector<discussion> get_discussions_by_created( const discussion_query& query )const;
		//vector<discussion> get_discussions_by_cashout( const discussion_query& query )const;
		//vector<discussion> get_discussions_by_payout( const discussion_query& query )const;
		//vector<discussion> get_discussions_by_votes( const discussion_query& query )const;
		//vector<discussion> get_discussions_by_children( const discussion_query& query )const;


		//  return the active discussions with the highest cumulative pending payouts without respect to category, total
		//  pending payout means the pending payout of all children as well.
		public JArray get_replies_by_last_update(string start_author, string start_permlink, uint limit)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(start_author);
			arrParams.Add(start_permlink);
			arrParams.Add(limit);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		// This method Is used to fetch all posts/comments by start_author that occur after before_date And start_permlink with up to limit being returned.
		//
		// If start_permlink Is empty then only before_date will be considered. If both are specified the eariler to the two metrics will be used. This
		// should allow easy pagination.
		public JArray get_discussions_by_author_before_date(string author, string start_permlink, DateTime before_date, uint limit)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(author);
			arrParams.Add(start_permlink);
			arrParams.Add(before_date);
			arrParams.Add(limit);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		// Account operations have sequence numbers from 0 to N where N Is the most recent operation. This method
		// returns operations in the range [from-limit, from]
		//
		// from - the absolute sequence number, -1 means most recent, limit Is the number of operations before from.
		// limit - the maximum number of items that can be queried (0 to 1000], must be less than from
		public JToken get_account_history(string account, Int64 from , UInt32 limit)
		{
			ArrayList arrParams = new ArrayList();
			arrParams.Add(account);
			arrParams.Add(from);
			arrParams.Add(limit);
			return call_api_token(CONDENSER_API + MethodBase.GetCurrentMethod().Name, arrParams);
		}
		#endregion 
	}
}
