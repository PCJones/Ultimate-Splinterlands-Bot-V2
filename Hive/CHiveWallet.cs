using System;
using System.Collections;
using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace HiveAPI.CS
{
	class CHiveWallet : CHiveAPI
	{
		#region Constructors
		public CHiveWallet(HttpClient oHttpClient, string strHostname = "127.0.0.1", ushort nPort = 8091) : base(oHttpClient, string.Format("http://{0}:{1}", strHostname, nPort))
		{
		}
		#endregion

		#region Public Methods

		// Returns info such As client version, git version Of graphene/fc, version of boost, openssl.
		// Returns compile time info And client And dependencies versions
		public JObject about()
		{
			return call_api(MethodBase.GetCurrentMethod().Name);
		}

		// Cancel an order created With create_order
		// Parameters:
		//    owner: The name Of the account owning the order To cancel_order (type String)
		//    orderid: The unique identifier assigned To the order by its creator (type: uint32_t)
		//    broadcast: true if you wish to broadcast the transaction (type: bool) 
		public JObject cancel_order(string owner, uint orderid, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(owner);
			arrParams.Add(orderid);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JObject challenge(string challenger, string challenged, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(challenger);
			arrParams.Add(challenged);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JObject change_recovery_account(string owner, string new_recovery_account, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(owner);
			arrParams.Add(new_recovery_account);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

        public JObject claim_reward_balance(string account, string reward_steem, string reward_sbd, string reward_vests,bool broadcast = true)
        {
            ArrayList arrParams = new();
            arrParams.Add(account);
            arrParams.Add(reward_steem);
            arrParams.Add(reward_sbd);
            arrParams.Add(reward_vests);
            arrParams.Add(broadcast);
            return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
        }

        // This method will convert SBD To STEEM at the current_median_history price
        // one week from the time it Is executed. This method depends upon there being
        // a valid price feed.
        // Parameters:
        //     from: The account requesting conversion Of its SBD i.e. "1.000 SBD"
        //    (type: String)
        //    amount: The amount Of SBD To convert (type: asset)
        //    broadcast: true if you wish to broadcast the transaction (type: bool)
        public JObject convert_sbd(string from, decimal amount, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(from);
			arrParams.Add(amount);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		// This method will genrate New owner, active, And memo keys For the New
		// account which will be controlable by this wallet. There Is a fee associated
		// With account creation that Is paid by the creator. The current account
		// creation fee can be found With the 'info' wallet command.
		// Parameters:
		//    creator: The account creating the New account (type: String)
		//    new_account_name: The name Of the New account (type: String)
		//    json_meta: JSON Metadata associated With the New account (type: String)
		//    broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject create_account(string creator, string new_account_name, string json_meta, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(creator);
			arrParams.Add(new_account_name);
			arrParams.Add(json_meta);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//This method Is used by faucets To create New accounts For other users which
		//must provide their desired keys. The resulting account may Not be
		//controllable by this wallet. There Is a fee associated With account
		//creation that Is paid by the creator. The current account creation fee can
		//be found With the 'info' wallet command.
		//Parameters:
		//     creator: The account creating the New account (type: String)
		//    newname: The name Of the New account (type: String)
		//    json_meta: JSON Metadata associated With the New account (type: String)
		//    owner: Public owner key Of the New account (type: public_key_type)
		//    active: Public active key Of the New account (type: public_key_type)
		//    posting: Public posting key Of the New account (type: public_key_type)
		//    memo: Public memo key Of the New account (type: public_key_type)
		//    broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject create_account_with_keys(string creator, string newname, string json_meta, string owner, string active, string posting, string memo, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(creator);
			arrParams.Add(newname);
			arrParams.Add(json_meta);
			arrParams.Add(owner);
			arrParams.Add(active);
			arrParams.Add(posting);
			arrParams.Add(memo);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Creates a limit order at the price amount_to_sell / min_to_receive And will
		//deduct amount_to_sell from account
		//Parameters:
		//     owner: The name Of the account creating the order (type: String)
		//    order_id: Is a unique identifier assigned by the creator of the order,
		//              it can be reused after the order has been filled (type: uint32_t)
		//    amount_to_sell: The amount Of either SBD Or STEEM you wish To sell (type: asset)
		//    min_to_receive: The amount Of the other asset you will receive at a minimum (type: asset)
		//    fill_or_kill: true if you want the order to be killed if it cannot immediately be filled (type: bool)
		//    expiration: the time the order should expire If it has Not been filled (type: uint32_t)
		//    broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject create_order(string owner, uint order_id, decimal amount_to_sell, decimal min_to_receive, bool fill_or_kill, uint expiration, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(owner);
			arrParams.Add(order_id);
			arrParams.Add(amount_to_sell);
			arrParams.Add(min_to_receive);
			arrParams.Add(fill_or_kill);
			arrParams.Add(expiration);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JObject decline_voting_rights(string account, bool decline, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(account);
			arrParams.Add(decline);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public string decrypt_memo(string memo)
		{
			ArrayList arrParams = new();
			arrParams.Add(memo);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams).ToString();
		}

		//Marks one account As following another account. Requires the posting authority Of the follower.
		//
		//Parameters:
		//     what: - a set of things to follow: posts, comments, votes, ignore (type: Set<String>)
		public JObject follow(string follower, string following, ArrayList what, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(follower);
			arrParams.Add(following);
			arrParams.Add(what);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Returns information about the given account.
		//Parameters:
		//     account_name: the name Of the account To provide information about (type: String)
		//Returns
		//    the Public account data stored In the blockchain
		public JObject get_account(string account_name)
		{
			ArrayList arrParams = new();
			arrParams.Add(account_name);

			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Account operations have sequence numbers from 0 To N where N Is the most
		//recent operation. This method returns operations In the range [from-limit,from]
		//
		//Parameters:
		//     account: - account whose history will be returned (type: String)
		//    from: - the absolute sequence number, -1 means most recent, limit Is
		//    the number Of operations before from. (type: uint32_t)
		//    limit: - the maximum number of items that can be queried (0 to 1000], must be less than from (type: uint32_t)
		public JToken get_account_history(string account, uint from, uint limit)
		{
			ArrayList arrParams = new();
			arrParams.Add(account);
			arrParams.Add(from);
			arrParams.Add(limit);
			return call_api_token(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Returns the list Of witnesses producing blocks In the current round (21 Blocks)
		public JArray get_active_witnesses()
		{
			return call_api_array(MethodBase.GetCurrentMethod().Name);
		}

		//Returns the information about a block
		//
		//Parameters:
		//num: Block num(type:   uint32_t)
		//
		//Returns
		//    Public block data On the blockchain
		public JObject get_block(uint num)
		{
			ArrayList arrParams = new();
			arrParams.Add(num);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Returns conversion requests by an account
		//
		//Parameters:
		//     owner: Account name Of the account owning the requests (type: String)
		//
		//Returns
		//    All pending conversion requests by account
		public JArray get_conversion_requests(string owner)
		{
			ArrayList arrParams = new();
			arrParams.Add(owner);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public string get_encrypted_memo(string from, string to, string memo)
		{
			ArrayList arrParams = new();
			arrParams.Add(from);
			arrParams.Add(to);
			arrParams.Add(memo);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams).ToString();
		}

		//return the current price feed history
		public JObject get_feed_history()
		{
			return call_api(MethodBase.GetCurrentMethod().Name);
		}

		public JArray get_inbox(string account, DateTime newest, uint limit)
		{
			ArrayList arrParams = new();
			arrParams.Add(account);
			arrParams.Add(newest);
			arrParams.Add(limit);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Returns the queue Of pow miners waiting To produce blocks.
		public JArray get_miner_queue()
		{
			return call_api_array(MethodBase.GetCurrentMethod().Name);
		}

		//Gets the current order book For STEEM:SBD
		//
		//Parameters:
		//     limit: Maximum number Of orders To return For bids And asks. Max Is 1000. (type: uint32_t)
		public JObject get_order_book(uint limit)
		{
			ArrayList arrParams = new();
			arrParams.Add(limit);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JArray get_outbox(string account, DateTime newest, uint limit)
		{
			ArrayList arrParams = new();
			arrParams.Add(account);
			arrParams.Add(newest);
			arrParams.Add(limit);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JArray get_owner_history(string strAccount)
		{
			ArrayList arrParams = new();
			arrParams.Add(strAccount);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public string get_private_key(string pubkey)
		{
			ArrayList arrParams = new();
			arrParams.Add(pubkey);
			return call_api_value(MethodBase.GetCurrentMethod().Name, arrParams).ToString();
		}

		// Get the WIF Private key corresponding To a Public key. The Private key must already be In the wallet.
		// Parameters:
		// role: - active | owner | posting | memo (type: String)
		public JObject get_private_key_from_password(string account, string role, string password)
		{
			ArrayList arrParams = new();
			arrParams.Add(account);
			arrParams.Add(role);
			arrParams.Add(password);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		// Returns an uninitialized Object representing a given blockchain operation.
		//
		//This returns a Default-initialized Object Of the given type; it can be used
		//during early development Of the wallet When we don't yet have custom
		//commands for creating all of the operations the blockchain supports.
		//
		//Any operation the blockchain supports can be created Using the transaction
		//builder's 'add_operation_to_builder_transaction()' , but to do that from
		//the CLI you need To know what the JSON form Of the operation looks Like.
		//This will give you a template you can fill In. It's better than nothing.
		//
		//Parameters:
		//     operation_type: the type Of operation To Return, must be one Of the
		//    operations defined In 'steemit/chain/operations.hpp' (e.g., "global_parameters_update_operation") (type: String)
		//
		//Returns
		//    a Default-constructed operation of the given type
		public JObject get_prototype_operation(string operation_type)
		{
			ArrayList arrParams = new();
			arrParams.Add(operation_type);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Returns the state info associated With the URL
		public JObject get_state(string url)
		{
			ArrayList arrParams = new();
			arrParams.Add(url);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Returns transaction by ID.
		public JObject get_transaction(string trx_id)
		{
			ArrayList arrParams = new();
			arrParams.Add(trx_id);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JArray get_withdraw_routes(string account, string type)
		{
			ArrayList arrParams = new();
			arrParams.Add(account);
			arrParams.Add(type);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    Returns information about the given witness.
		//
		//    Parameters:
		//        owner_account: the name Or id Of the witness account owner, Or the id of the witness (type: String)
		//
		//    Returns
		//        the information about the witness stored In the block chain
		public JObject get_witness(string owner_account)
		{
			ArrayList arrParams = new();
			arrParams.Add(owner_account);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    Returns detailed help On a Single API command.
		//
		//    Parameters:
		//     method: the name Of the API command you want help With (type: Const String &)
		//
		//    Returns
		//        a multi-line String suitable For displaying On a terminal
		public string gethelp(string method)
		{
			ArrayList arrParams = new();
			arrParams.Add(method);
			return call_api_value(MethodBase.GetCurrentMethod().Name, arrParams).ToString();
		}

		//Returns a list Of all commands supported by the wallet API.
		//
		//This lists Each command, along With its arguments And return types. For
		//more detailed help On a Single command, use 'get_help()'
		//
		//Returns
		//    a multi-line String suitable For displaying On a terminal
		public string help()
		{
			return call_api_value(MethodBase.GetCurrentMethod().Name).ToString();
		}

		//    Imports a WIF Private Key into the wallet To be used To sign transactionsby an account.
		//
		//    example: import_key 5KQwrPbwdL6PhXujxW37FSSQZ1JiwsST4cqQzDeyXtP79zkvFD3
		//
		//    Parameters:
		//         wif_key: the WIF Private Key To import (type: String)
		public bool import_key(string wif_key)
		{
			ArrayList arrParams = new();
			arrParams.Add(wif_key);
			return (bool)call_api_value(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Returns info about the current state Of the blockchain
		public JObject info()
		{
			return call_api(MethodBase.GetCurrentMethod().Name);
		}

		//Checks whether the wallet Is locked (Is unable To use its Private keys).
		//This state can be changed by calling 'lock()' or 'unlock()'.
		//
		//Returns
		//    true if the wallet Is locked
		public bool is_locked()
		{
			return (bool)call_api_value(MethodBase.GetCurrentMethod().Name);
		}

		//Checks whether the wallet has just been created And has Not yet had a password set.
		//Calling 'set_password' will transition the wallet to the locked state.
		//
		//Returns
		//    true if the wallet Is New
		public bool is_new()
		{
			return (bool)call_api_value(MethodBase.GetCurrentMethod().Name);
		}

		//    Lists all accounts registered In the blockchain. This returns a list Of all
		//    account names And their account ids, sorted by account name.
		//
		//    Use the 'lowerbound' and limit parameters to page through the list. To
		//    retrieve all accounts, start by setting 'lowerbound' to the empty string
		//    '""', and then each iteration, pass the last account name returned as the
		//    'lowerbound' for the next 'list_accounts()' call.
		//
		//    Parameters:
		//         lowerbound: the name Of the first account To Return. If the named
		//        account does Not exist, the list will start at the account that
		//        comes after 'lowerbound' (type: const string &)
		//         limit: the maximum number Of accounts To return (max: 1000) (type:
		//        uint32_t)
		//
		//    Returns
		//        a list Of accounts mapping account names To account ids
		public JArray list_accounts(string lowerbound, uint limit)
		{
			ArrayList arrParams = new();
			arrParams.Add(lowerbound);
			arrParams.Add(limit);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Dumps all Private keys owned by the wallet.
		//
		//The keys are printed In WIF format. You can import these keys into another
		//wallet using 'import_key()'
		//
		//Returns
		//    a map containing the Private keys, indexed by their Public key
		public JToken list_keys()
		{
			return call_api_token(MethodBase.GetCurrentMethod().Name);
		}

		//Gets the account information For all accounts For which this wallet has aPrivate key

		public JArray list_my_accounts()
		{
			return call_api_array(MethodBase.GetCurrentMethod().Name);
		}

		//    Lists all witnesses registered In the blockchain. This returns a list Of
		//    all account names that own witnesses, And the associated witness id, sorted
		//    by name. This lists witnesses whether they are currently voted In Or Not.
		//
		//    Use the 'lowerbound' and limit parameters to page through the list. To
		//    retrieve all witnesss, start by setting 'lowerbound' to the empty string
		//    '""', and then each iteration, pass the last witness name returned as the
		//    'lowerbound' for the next 'list_witnesss()' call.
		//
		//    Parameters:
		//         lowerbound: the name Of the first witness To Return. If the named
		//        witness does Not exist, the list will start at the witness that
		//        comes after 'lowerbound' (type: const string &)
		//         limit: the maximum number Of witnesss To return (max: 1000) (type: uint32_t)
		//
		//    Returns
		//        a list Of witnesss mapping witness names To witness ids
		//
		public JArray list_witnesses(string lowerbound, uint limit)
		{
			ArrayList arrParams = new();
			arrParams.Add(lowerbound);
			arrParams.Add(limit);
			return call_api_array(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    Loads a specified Graphene wallet.
		//
		//    The current wallet Is closed before the New wallet Is loaded.
		//
		//    Parameters:
		//         wallet_filename: the filename Of the wallet JSON file To load. If
		//        'wallet_filename' is empty, it reloads the existing wallet file (type String)
		//
		//    Returns
		//        true if the specified wallet Is loaded
		public bool load_wallet_file(string wallet_filename)
		{
			ArrayList arrParams = new();
			arrParams.Add(wallet_filename);
			return (bool)call_api_value(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Locks the wallet immediately.
		public void Lock()
		{
			call_api_sub(MethodBase.GetCurrentMethod().Name);
		}

		public void network_add_nodes(ArrayList nodes)
		{
			call_api_sub(MethodBase.GetCurrentMethod().Name);
		}

		public JArray network_get_connected_peers()
		{
			return call_api_array(MethodBase.GetCurrentMethod().Name);
		}

		//    Transforms a brain key To reduce the chance Of errors When re-entering the
		//    key from memory.
		//
		//    This takes a user-supplied brain key And normalizes it into the form used
		//    For generating private keys. In particular, this upper-cases all ASCII
		//    characters And collapses multiple spaces into one.
		//
		//    Parameters:
		//     key: the brain key As supplied by the user (type: String)
		//
		//    Returns
		//        the brain key In its normalized form
		public string normalize_brain_key(string key)
		{
			ArrayList arrParams = new();
			arrParams.Add(key);
			return call_api_value(MethodBase.GetCurrentMethod().Name, arrParams).ToString();
		}

		//    Post Or update a comment.
		//
		//    Parameters:
		//        author: the name Of the account authoring the comment (type: String)
		//        permlink: the accountwide unique permlink For the comment (type
		//        String)
		//        parent_author: can be null If this Is a top level comment (type
		//        String)
		//        parent_permlink: becomes category If parent_author Is "" (type: String)
		//        title: the title Of the comment (type: String)
		//        body: the body Of the comment (type: String)
		//        json: the json metadata Of the comment (type: String)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject post_comment(string author, string permlink, string parent_author, string parent_permlink, string title, string body, string json, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(author);
			arrParams.Add(permlink);
			arrParams.Add(parent_author);
			arrParams.Add(parent_permlink);
			arrParams.Add(title);
			arrParams.Add(body);
			arrParams.Add(json);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}


		public JObject prove(string challenged, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(challenged);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}


		//    A witness can Public a price feed For the STEEM:SBD market. The median
		//    price feed Is used To process conversion requests from SBD To STEEM.
		//
		//    Parameters:
		//         witness: The witness publishing the price feed (type: String)
		//        exchange_rate: The desired exchange rate (type: price)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject publish_feed(string witness, decimal exchange_rate, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(witness);
			arrParams.Add(exchange_rate);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}


		public JObject recover_account(string account_to_recover, Hashtable recent_authority, Hashtable new_authority, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(account_to_recover);
			arrParams.Add(recent_authority);
			arrParams.Add(new_authority);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JObject request_account_recovery(string recovery_account, string account_to_recover, Hashtable new_authority, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(recovery_account);
			arrParams.Add(account_to_recover);
			arrParams.Add(new_authority);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    Saves the current wallet To the given filename.
		//
		//    Parameters:
		//wallet_filename: the filename Of the New wallet JSON file To create Or
		//        overwrite. If 'wallet_filename' is empty, save to the current
		//        filename. (type: String)
		public void save_wallet_file(string wallet_filename)
		{
			ArrayList arrParams = new();
			arrParams.Add(wallet_filename);
			call_api_sub(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JObject send_private_message(string from, string to, string subject, string body, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(from);
			arrParams.Add(to);
			arrParams.Add(subject);
			arrParams.Add(body);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Sets a New password On the wallet.
		//The wallet must be either 'new' or 'unlocked' to execute this command.
		public void set_password(string password)
		{
			ArrayList arrParams = new();
			arrParams.Add(password);
			call_api_sub(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public void set_transaction_expiration(uint seconds)
		{
			ArrayList arrParams = new();
			arrParams.Add(seconds);
			call_api_sub(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    Set the voting proxy For an account.
		//
		//    If a user does Not wish To take an active part In voting, they can choose
		//    to allow another account to vote their stake.
		//
		//    Setting a vote proxy does Not remove your previous votes from the
		//    blockchain, they remain there but are ignored. If you later null out your
		//    vote proxy, your previous votes will take effect again.
		//
		//    This setting can be changed at any time.
		//
		//    Parameters:
		//         account_to_modify: the name Or id Of the account To update (type
		//        String)
		//        proxy: the name Of account that should proxy To, Or empty String To
		//        have no proxy (type: String)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject set_voting_proxy(string account_to_modify, string proxy, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(account_to_modify);
			arrParams.Add(proxy);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}


		public JObject set_withdraw_vesting_route(string from, string to, ushort percent, bool auto_vest, bool broadcast)
		{
			ArrayList arrParams = new();
			arrParams.Add(from);
			arrParams.Add(to);
			arrParams.Add(percent);
			arrParams.Add(auto_vest);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//Suggests a safe brain key To use For creating your account.
		//'create_account_with_brain_key()' requires you to specify a 'brain key', a
		//Long passphrase that provides enough entropy to generate cyrptographic
		//keys. This function will suggest a suitably random string that should be
		//easy to write down (And, with effort, memorize).
		//
		//Returns
		//    a suggested brain_key
		public string suggest_brain_key()
		{
			return call_api_value(MethodBase.GetCurrentMethod().Name).ToString();
		}

		//    Transfer funds from one account To another. STEEM And SBD can be
		//    transferred.
		//
		//    Parameters:
		//         from: The account the funds are coming from (type: String)
		//        to: The account the funds are going To (type: String)
		//        amount: The funds being transferred. i.e. "100.000 STEEM" (type: asset)
		//        memo: A memo For the transactionm, encrypted With the To account's
		//        Public memo key (type: String)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject transfer(string from, string to, string amount, string memo, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(from);
			arrParams.Add(to);
			arrParams.Add(amount);
			arrParams.Add(memo);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    Transfer STEEM into a vesting fund represented by vesting shares (VESTS).
		//    VESTS are required To vesting For a minimum Of one coin year And can be
		//    withdrawn once a week over a two year withdraw period. VESTS are Protected
		//    against dilution up until 90% Of STEEM Is vesting.
		//
		//    Parameters:
		//         from: The account the STEEM Is coming from (type: String)
		//        to: The account getting the VESTS (type: String)
		//        amount: The amount Of STEEM To vest i.e. "100.00 STEEM" (type: asset)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)

		public JObject transfer_to_vesting(string from, string to, decimal amount, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(from);
			arrParams.Add(to);
			arrParams.Add(amount);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		// Transfers into savings happen immediately
		//
		public JObject transfer_to_savings(string from, string to, string amount, string memo, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(from);
			arrParams.Add(to);
			arrParams.Add(amount);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}


		// Transfers transfers from savings take 72 hours
		// request_id - an unique ID assigned by from account, the id is used to cancel the operation and can be reused after the transfer completes
		//
		public JObject transfer_from_savings(string from, uint request_id, string to, string amount, string memo, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(from);
			arrParams.Add(request_id);
			arrParams.Add(to);
			arrParams.Add(amount);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		// @param from the account that initiated the transfer
		// @param request_id the id used in transfer_from_savings
		//
		public JObject cancel_transfer_from_savings(string from, uint request_id, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(from);
			arrParams.Add(request_id);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    The wallet remain unlocked until the 'lock' is called or the program exits.
		//
		//    Parameters:
		//         password: the password previously Set With 'set_password()' (type: String)
		public void unlock(string password)
		{
			ArrayList arrParams = new();
			arrParams.Add(password);
			call_api_sub(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    This method updates the keys Of an existing account.
		//
		//    Parameters:
		//         accountname: The name Of the account (type: String)
		//        json_meta: New JSON Metadata to be associated with the account (type
		//        String)
		//        owner: New public owner key for the account (type: public_key_type)
		//        active: New public active key for the account (type: public_key_type)
		//        posting: New public posting key for the account (type: public_key_type)
		//        memo: New public memo key for the account (type: public_key_type)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject update_account(string accountname, string json_meta, string owner, string active, string posting, string memo, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(accountname);
			arrParams.Add(json_meta);
			arrParams.Add(owner);
			arrParams.Add(active);
			arrParams.Add(memo);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    update_account_auth_key(string account_name, authority_type type, public_key_type key, weight_type weight, bool broadcast)
		//
		//    This method updates the key Of an authority For an exisiting account.
		//    Warning: You can create impossible authorities Using this method. The
		//    method will fail If you create an impossible owner authority, but will
		//    allow impossible active And posting authorities.
		//
		//    Parameters:
		//         account_name: The name Of the account whose authority you wish To
		//        update (type: String)
		//        type: The authority type. e.g. owner, active, Or posting (type:
		//        authority_type)
		//        key: The Public key To add To the authority (type: public_key_type)
		//        weight: The weight the key should have In the authority. A weight Of 0
		//        indicates the removal Of the key. (type: weight_type)
		//        broadcast: true if you wish to broadcast the transaction. (type: bool)
		public JObject update_account_auth_account(string account_name, string type, string auth_account, ushort weight, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(account_name);
			arrParams.Add(type);
			arrParams.Add(auth_account);
			arrParams.Add(weight);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		public JObject update_account_auth_key(string account_name, string type, string key, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(account_name);
			arrParams.Add(type);
			arrParams.Add(key);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    This method updates the weight threshold Of an authority For an account.
		//    Warning: You can create impossible authorities Using this method As well As
		//    implicitly met authorities. The method will fail If you create an
		//    implicitly true authority And if you create an impossible owner authoroty,
		//    but will allow impossible active And posting authorities.
		//
		//    Parameters:
		//         account_name: The name Of the account whose authority you wish To
		//        update (type: String)
		//        type: The authority type. e.g. owner, active, Or posting (type:
		//        authority_type)
		//        threshold: The weight threshold required For the authority To be met
		//        (type: uint32_t)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject update_account_auth_threshold(string account_name , string type , uint threshold, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(account_name);
			arrParams.Add(type);
			arrParams.Add(threshold);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    This method updates the memo key Of an account
		//
		//    Parameters:
		//        account_name: The name Of the account you wish To update (type: String)
		//        key: The New memo public key (type: public_key_type)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject update_account_memo_key(string account_name, string key, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(account_name);
			arrParams.Add(key);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    This method updates the account JSON metadata
		//
		//    Parameters:
		//         account_name: The name Of the account you wish To update (type: String)
		//        json_meta: The New JSON metadata for the account. This overrides
		//        existing metadata(Type: String)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject update_account_meta(string account_name, string json_meta, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(account_name);
			arrParams.Add(json_meta);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    Update a witness Object owned by the given account.
		//
		//    Parameters:
		//         witness_name: The name Of the witness's owner account.Also accepts the
		//        ID of the owner account Or the ID of the witness. (type: String)
		//        url: Same as for create_witness. The empty string makes it remain the
		//        same. (type: String)
		//        block_signing_key: The New block signing public key. The empty string
		//        makes it remain the same. (type: public_key_type)
		//        props: The chain properties the witness Is voting On. (type: Const
		//        chain_properties &)
		//        broadcast: true if you wish to broadcast the transaction. (type: bool)
		public JObject update_witness(string witness_name, string url, string block_signing_key, JArray props, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(witness_name);
			arrParams.Add(url);
			arrParams.Add(block_signing_key);
			arrParams.Add(props);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    Vote on a comment to be paid STEEM
		//
		//    Parameters:
		//         voter: The account voting (type: String)
		//        author: The author Of the comment To be voted On (type: String)
		//        permlink: The permlink Of the comment To be voted On. (author,
		//        permlink) Is a unique pair (type: String)
		//        weight: The weight [-100,100] Of the vote (type: int16_t)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject vote(string voter, string author, string permlink, short weight, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(voter);
			arrParams.Add(author);
			arrParams.Add(permlink);
			arrParams.Add(weight);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    Vote for a witness to become a block producer. By default an account has
		//    Not voted positively Or negatively for a witness. The account can either
		//    vote for with positively votes Or against with negative votes. The vote
		//    will remain until updated With another vote. Vote strength Is determined by
		//    the accounts vesting shares.
		//
		//    Parameters:
		//         account_to_vote_with: The account voting For a witness (type: String)
		//        witness_to_vote_for: The witness that Is being voted For (type: String)
		//        approve: true if the account Is voting for the account to be able to be
		//        a block produce (type: bool)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)

		public JObject vote_for_witness(string account_to_vote_with, string witness_to_vote_for, bool approve, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(account_to_vote_with);
			arrParams.Add(witness_to_vote_for);
			arrParams.Add(approve);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}

		//    Set up a vesting withdraw request. The request Is fulfilled once a week
		//    over the Next two year (104 weeks).
		//
		//    Parameters:
		//         from: The account the VESTS are withdrawn from (type: String)
		//        vesting_shares: The amount Of VESTS To withdraw over the Next two
		//        years. Each week (amount/104) shares are withdrawn And depositted
		//        back as STEEM. i.e. "10.000000 VESTS" (type: asset)
		//        broadcast: true if you wish to broadcast the transaction (type: bool)
		public JObject withdraw_vesting(string from, decimal vesting_shares, bool broadcast = true)
		{
			ArrayList arrParams = new();
			arrParams.Add(from);
			arrParams.Add(vesting_shares);
			arrParams.Add(broadcast);
			return call_api(MethodBase.GetCurrentMethod().Name, arrParams);
		}
		#endregion
	}
}
