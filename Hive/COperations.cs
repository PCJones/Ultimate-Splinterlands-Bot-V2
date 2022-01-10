using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cryptography.ECDSA;
using Newtonsoft.Json;

namespace HiveAPI.CS
{
	[AttributeUsage(AttributeTargets.Field)] public class OptionalField : Attribute {}

	public class AssetJsonConverter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(value.ToString());
		}
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}
		public override bool CanConvert(Type objectType)
		{
			throw new NotImplementedException();
		}
	}

	[JsonConverter(typeof(AssetJsonConverter))] public class Asset
	{
		public decimal amount;
		public string symbol;

		private static string CheckSymbol(string strSymbol)
		{
			string[] aSymbols = { "HIVE", "HBD", "TESTS", "TBD", "VESTS" };

			if (string.IsNullOrEmpty(strSymbol) || !aSymbols.Contains(strSymbol))
			{
				throw new Exception(string.Format("Invalid asset symbol: {0}", strSymbol));
			}
			return strSymbol;
		}

		public Asset(decimal strAmount, string strSymbol)  
		{
			amount = strAmount;
			symbol = CheckSymbol(strSymbol);
		}

		public Asset(string str)
		{
			string[] astr = str.Split(' ');

			if (astr.Length < 2)
			{
				throw new Exception("Invalid asset string");
			}
			if (string.IsNullOrEmpty(astr[0]))
			{
				throw new Exception("Invalid asset amount (null or empty)");
			}
			amount = decimal.Parse(astr[0], CultureInfo.InvariantCulture);
			symbol = CheckSymbol(astr[1]);
		}

		public int GetPrecision()
		{
			string[] asymbols = { "HIVE", "HBD", "TESTS", "TBD" };
			return asymbols.Contains(symbol) ? 3 : 6;
		}

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "{0:0." + new string('0', GetPrecision()) + "} {1}", amount, symbol);
		}
	}

	public class PublicKeyJsonConverter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(value.ToString());
		}
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			return new PublicKey((string)reader.Value);
		}
		public override bool CanConvert(Type objectType)
		{
			throw new NotImplementedException();
		}
	}
	[JsonConverter(typeof(PublicKeyJsonConverter))] public class PublicKey
	{
		public string key;

		public PublicKey(string strKey )
		{
			key = strKey;
		}
		public override string ToString()
		{
			return key;
		}
		public byte[] Decode()
		{
			return Base58.RemoveCheckSum(Base58.Decode(key.Substring(3)));
		}
	}
	public class Price
	{
		public Asset @base;
		public Asset quote;
	}

	public class AccountAuthsJsonConverter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			Dictionary<string, ushort> auths = (Dictionary<string, ushort>)value;

			writer.WriteStartArray();
			foreach (KeyValuePair<string, ushort> auth in auths)
			{
				writer.WriteStartArray();
				writer.WriteValue(auth.Key);
				writer.WriteValue(auth.Value);
				writer.WriteEndArray();
			}
			writer.WriteEndArray();
		}
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			return new PublicKey((string)reader.Value);
		}
		public override bool CanConvert(Type objectType)
		{
			throw new NotImplementedException();
		}
	}

	public class KeyAuthsJsonConverter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			Dictionary<PublicKey, ushort> auths = (Dictionary<PublicKey, ushort>)value;

			writer.WriteStartArray();
			foreach (KeyValuePair<PublicKey, ushort> auth in auths)
			{
				writer.WriteStartArray();
				writer.WriteValue(auth.Key.ToString());
				writer.WriteValue(auth.Value);
				writer.WriteEndArray();
			}
			writer.WriteEndArray();
		}
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			return new PublicKey((string)reader.Value);
		}
		public override bool CanConvert(Type objectType)
		{
			throw new NotImplementedException();
		}
	}

	public class Authority
	{
		public uint weight_threshold;
		[JsonConverter(typeof(AccountAuthsJsonConverter))] public Dictionary<string, ushort> account_auths = new();
		[JsonConverter(typeof(KeyAuthsJsonConverter))] public Dictionary<PublicKey, ushort> key_auths = new();
	}

	public class ChainProperties
	{
		public Asset account_creation_fee;
		public uint maximum_block_size;
		public ushort hbd_interest_rate;
	}

	public class WitnessProperties
	{
		public Asset account_creation_fee;
		public uint account_voidsidy_budget;
		public uint account_voidsidy_decay;
		public uint maximum_block_size;
		public ushort hbd_interest_rate;
		public Price hbd_exchange_rate;
		public string url;
		public PublicKey new_signing_key;
	}

	public class OpJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			throw new NotImplementedException();
		}
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			Op op = (Op)value;
			writer.WriteStartArray();
			writer.WriteValue(op.name);
			serializer.Serialize(writer,op.payload);
			writer.WriteEndArray();
		}
	}

	[JsonConverter(typeof(OpJsonConverter))] public class Op
	{
		public string name;
		public object payload;
	}

	public class COperations
	{
		public interface IOperationID
		{
			int opid { get; }
		}
		public class vote : IOperationID
		{
			public int opid => 0;
			public string voter;
			public string author;
			public string permlink;
			public short weight;
		}
		public class comment : IOperationID
		{
			public int opid => 1;
			public string parent_author;
			public string parent_permlink;
			public string author;
			public string permlink;
			public string title;
			public string body;
			public string json_metadata;
		}
		public class transfer : IOperationID
		{
			public int opid => 2;
			public string from;
			public string to;
			public Asset amount;
			public string memo;
		}
		public class transfer_to_vesting : IOperationID
		{
			public int opid => 3;
			public string from;
			public string to;
			public Asset amount;
		}
		public class withdraw_vesting : IOperationID
		{
			public int opid => 4;
			public string account;
			public Asset vesting_shares;
		}
		public class limit_order_create : IOperationID
		{
			public int opid => 5;
			public string owner;
			public uint orderid;
			public Asset amount_to_sell;
			public Asset min_to_receive;
			public bool fill_or_kill;
			public DateTime expiration;
		}
		public class limit_order_cancel : IOperationID
		{
			public int opid => 6;
			public string owner;
			public uint orderid;
		}
		public class feed_publish : IOperationID
		{
			public int opid => 7;
			public string publisher;
			public Price exchange_rate;
		}
		public class convert : IOperationID
		{
			public int opid => 8;
			public string owner;
			public uint requestid;
			public Asset amount;
		}
		public class account_create : IOperationID
		{
			public int opid => 9;
			public Asset fee;
			public string creator;
			public string new_account_name;
			public Authority owner;
			public Authority active;
			public Authority posting;
			public PublicKey memo_key;
			public string json_metadata;
		}
		public class account_update : IOperationID
		{
			public int opid => 10;
			public string account;
			[OptionalField] public Authority owner;
			[OptionalField] public Authority active;
			[OptionalField] public Authority posting;
			public PublicKey memo_key;
			public string json_metadata;
		}
		public class witness_update : IOperationID
		{
			public int opid => 11;
			public string owner;
			public string url;
			public PublicKey block_signing_key;
			public ChainProperties props;
			public Asset fee;
		}
		public class account_witness_vote : IOperationID
		{
			public int opid => 12;
			public string account;
			public string witness;
			public bool approve;
		}
		public class account_witness_proxy : IOperationID
		{
			public int opid => 13;
			public string account;
			public string proxy;
		}
		// pow_operation (id=14) has been deprecated
		public class custom : IOperationID
		{
			public int opid => 15;
			public string required_auths;
			public ushort id;
			public byte[] data;
		}
		// report_over_production_operation (id=16) has been deprecated
		public class delete_comment : IOperationID
		{
			public int opid => 17;
			public string author;
			public string permlink;
		}
		public class custom_json : IOperationID
		{
			public int opid => 18;
			public string[] required_auths;
			public string[] required_posting_auths;
			public string id;
			public string json;
		}
		public class comment_options : IOperationID
		{
			public int opid => 19;
			public string author;
			public string permlink;
			public Asset max_accepted_payout;
			public ushort percent_hbd;
			public bool allow_votes;
			public bool allow_curation_rewards;
			public object[] extensions = Array.Empty<object>();
		}

		public class set_withdraw_vesting_route : IOperationID
		{
			public int opid => 20;
			public string from_account;
			public string to_account;
			public ushort percent;
			public bool auto_vest;
		}
		public class limit_order_create2 : IOperationID
		{
			public int opid => 21;
			public string owner;
			public uint orderid;
			public Asset amount_to_sell;
			public bool fill_or_kill;
			public Price exchange_rate;
			public DateTime expiration;
		}
		public class claim_account : IOperationID
		{
			public int opid => 22;
			public string creator;
			public Asset fee;
			public object[] extensions = Array.Empty<object>();
		}
		public class create_claimed_account : IOperationID
		{
			public int opid => 23;
			public string creator;
			public string new_account_name;
			public Authority owner;
			public Authority active;
			public Authority posting;
			public PublicKey memo_key;
			public string json_metadata;
			public object[] extensions = Array.Empty<object>();
		}
		public class request_account_recovery : IOperationID
		{
			public int opid => 24;
			public string recovery_account;
			public string account_to_recover;
			public Authority new_owner_authority;
			public object[] extensions = Array.Empty<object>();
		}
		public class recover_account : IOperationID
		{
			public int opid => 25;
			public string account_to_recover;
			public Authority new_owner_authority;
			public Authority recent_owner_authority;
			public object[] extensions = Array.Empty<object>();
		}
		public class change_recovery_account : IOperationID
		{
			public int opid => 26;
			public string account_to_recover;
			public string new_recovery_account;
			public object[] extensions = Array.Empty<object>();
		}

		public class escrow_transfer : IOperationID
		{
			public int opid => 27;
			public string from;
			public string to;
			public string agent;
			public uint escrow_id;
			public Asset hbd_amount;
			public Asset hive_amount;
			public Asset fee;
			public DateTime ratification_deadline;
			public DateTime escrow_expiration;
			public string json_meta;
		}
		public class escrow_dispute : IOperationID
		{
			public int opid => 28;
			public string from;
			public string to;
			public string agent;
			public string who;
			public uint escrow_id;
		}
		public class escrow_release : IOperationID
		{
			public int opid => 29;
			public string from;
			public string to;
			public string agent;
			public string who;
			public string receiver;
			public uint escrow_id;
			public Asset hbd_amount;
			public Asset hive_amount;
		}
		// pow2_operation (id=30) has been deprecated
		public class escrow_approve : IOperationID
		{
			public int opid => 31;
			public string from;
			public string to;
			public string agent;
			public string who;
			public uint escrow_id;
			public bool approve;
		}
		public class transfer_to_savings : IOperationID
		{
			public int opid => 32;
			public string from;
			public string to;
			public Asset amount;
			public string memo;
		}
		public class transfer_from_savings : IOperationID
		{
			public int opid => 33;
			public string from;
			public uint request_id;
			public string to;
			public Asset amount;
			public string memo;
		}
		public class cancel_transfer_from_savings : IOperationID
		{
			public int opid => 34;
			public string from;
			public uint request_id;
		}
		public class custom_binary : IOperationID
		{
			public int opid => 35;
			public string required_owner_auths;
			public string required_active_auths;
			public string required_posting_auths;
			public Authority[] required_auths;
			public string id;
			public byte[] data;
		}
		public class decline_voting_rights : IOperationID
		{
			public int opid => 36;
			public string account;
			public bool decline;
		}
		public class reset_account : IOperationID
		{
			public int opid => 37;
			[JsonProperty("reset_account")] public string account;
			public string account_to_reset;
			public Authority new_owner_authority;

		}
		public class set_reset_account : IOperationID
		{
			public int opid => 38;
			public string account;
			public string current_reset_account;
			public string reset_account;
		}
		public class claim_reward_balance : IOperationID
		{
			public int opid => 39;
			public string account;
			public Asset reward_hive;
			public Asset reward_hbd;
			public Asset reward_vests;
		}
		public class delegate_vesting_shares : IOperationID
		{
			public int opid => 40;
			public string delegator;
			public string delegatee;
			public Asset vesting_shares;
		}
		public class account_create_with_delegation : IOperationID
		{
			public int opid => 41;
			public Asset fee;
			public Asset delegation;
			public string creator;
			public string new_account_name;
			public Authority owner;
			public Authority active;
			public Authority posting;
			public PublicKey memo_key;
			public string json_metadata;
			public object[] extensions = Array.Empty<object>();
		}
		public class witness_set_properties : IOperationID
		{
			public int opid => 42;
			public string owner;
			public WitnessProperties props;
			public object[] extensions = Array.Empty<object>();
		}
		public class account_update2 : IOperationID
		{
			public int opid => 43;
			public string account;
			[OptionalField] public Authority owner;
			[OptionalField] public Authority active;
			[OptionalField] public Authority posting;
			[OptionalField] public PublicKey memo_key;
			public string json_metadata;
			public string posting_json_metadata;
			public object[] extensions = Array.Empty<object>();
		}
		public class create_proposal : IOperationID
		{
			public int opid => 44;
			public string creator;
			public string receiver;
			public DateTime start_date;
			public DateTime end_date;
			public Asset daily_pay;
			public string voidject;
			public string permlink;
			public object[] extensions = Array.Empty<object>();
		}
		public class update_proposal_votes : IOperationID
		{
			public int opid => 45;
			public string voter;
			public long[] proposal_ids;
			public bool approve;
			public object[] extensions = Array.Empty<object>();
		}
		public class remove_proposal : IOperationID
		{
			public int opid => 46;
			public string proposal_owner;
			public long[] proposal_ids;
			public object[] extensions = Array.Empty<object>();
		}
		public class update_proposal : IOperationID
		{
			public int opid => 47;
			public ulong proposal_id;
			public string creator;
			public Asset daily_pay;
			public string voidject;
			public string permlink;
			public object[] extensions = Array.Empty<object>();
		}
		public class collateralized_convert : IOperationID
		{
			public int opid => 48;
			public string owner;
			public uint requestid;
			public Asset amount;
		}
		public class recurrent_transfer : IOperationID
		{
			public int opid => 49;
			public string from;
			public string to;
			public Asset amount;
			public string memo;
			public ushort recurrence;
			public ushort executions;
			public object[] extensions = Array.Empty<object>();
	    }
	}
}
