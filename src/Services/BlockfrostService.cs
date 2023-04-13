using System.Collections.Generic;
using System.Text.Json;
using Blockfrost.Api;
using Blockfrost.Api.Extensions;
using Blockfrost.Api.Models;
using Blockfrost.Api.Services;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness.PlutusScripts;
using CardanoSharp.Wallet.TransactionBuilding;
using Microsoft.Extensions.DependencyInjection;
using static TestEnvironment.Setup.BlockfrostService.BlockfrostData;

namespace TestEnvironment.Setup;

public class BlockfrostService
{
    public string cardanoNetwork { get; set; } = default!;
    public string blockfrostAPIKey { get; set; } = default!;

    public ServiceProvider provider { get; set; }
    public IBlocksService blockfrostBlocksService { get; set; }
    public IAccountsService blockfrostAccountsService { get; set; }
    public IAddressesService blockfrostAddressesService { get; set; }
    public IAssetsService blockfrostAssetsService { get; set; }
    public ITransactionsService blockfrostTransactionsService { get; set; }
    public IEpochsService blockfrostEpochsService { get; set; }

    // Blockfrost Data
    public BlockfrostData blockfrostData { get; set; } = new BlockfrostData();

    public BlockfrostService()
    {
        DotNetEnv.Env.Load();
        this.cardanoNetwork = Environment.GetEnvironmentVariable("CARDANO_NETWORK")!;
        this.blockfrostAPIKey = Environment.GetEnvironmentVariable("BLOCKFORST_API_KEY")!;

        provider = new ServiceCollection()
            .AddBlockfrost(cardanoNetwork, blockfrostAPIKey)
            .BuildServiceProvider();
        blockfrostBlocksService = provider.GetRequiredService<IBlocksService>();
        blockfrostAccountsService = provider.GetRequiredService<IAccountsService>();
        blockfrostAddressesService = provider.GetRequiredService<IAddressesService>();
        blockfrostAssetsService = provider.GetRequiredService<IAssetsService>();
        blockfrostTransactionsService = provider.GetRequiredService<ITransactionsService>();
        blockfrostEpochsService = provider.GetRequiredService<IEpochsService>();
    }

    public async Task Initialize()
    {
        await blockfrostData.UpdateData(blockfrostBlocksService, blockfrostEpochsService);
    }

    public async Task<string?> SubmitTransaction(Transaction transaction)
    {
        try
        {
            byte[] signedTx = transaction.Serialize();
            using (MemoryStream stream = new MemoryStream(signedTx))
            {
                var txid = await blockfrostTransactionsService.PostTxSubmitAsync(stream);
                return txid;
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.ToString());
            return null;
        }
    }

    //---------------------------------------------------------------------------------------------------//
    // Block API Functions Not in SDK
    //---------------------------------------------------------------------------------------------------//

    public async Task<List<AddressAssetUtxoContentResponse>> GetAddressAssetUTXOs(
        string address,
        string asset,
        int? count = 100,
        int? page = 1,
        ESortOrder? order = ESortOrder.Desc
    )
    {
        string uri = "https://cardano-mainnet.blockfrost.io";
        if (this.cardanoNetwork == "preprod")
        {
            uri = "https://cardano-preprod.blockfrost.io";
        }
        string endpoint = $"/api/v0/addresses/{address}/utxos/{asset}";
        string parameters = $"count={count}&page={page}&order={order!.ToString()!.ToLower()}";

        RequestData requestData = new RequestData
        {
            uri = uri,
            endpoint = endpoint,
            httpMethod = HttpMethod.Get,
            headers = new Dictionary<string, string> { { "project_id", this.blockfrostAPIKey } },
            contentType = "application/json",
            parameters = parameters
        };

        HttpResponseMessage response = await APIService.SendRequest(requestData);
        List<AddressAssetUtxoContentResponse> assetUTXOs = await APIService.Content<
            List<AddressAssetUtxoContentResponse>
        >(response);
        return assetUTXOs;
    }

    public async Task<EvaluatedTransaction> EvaluateScriptTransaction(Transaction transaction)
    {
        try
        {
            byte[] signedTx = transaction.Serialize();
            var evaluation = await EvaluateScriptTransaction(signedTx);
            return evaluation;
        }
        catch (Blockfrost.Api.ApiException<Blockfrost.Api.BadRequestResponse> blockfrostException)
        {
            Console.WriteLine(blockfrostException.Result.Message.ToString());
            return null!;
        }
        catch (Blockfrost.Api.ApiException blockfrostException)
        {
            Console.WriteLine(blockfrostException.Message.ToString());
            return null!;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.ToString());
            return null!;
        }
    }

    public class BlockfrostData
    {
        // Protocol Parameters
        public EpochParamContentResponse protocolParameters { get; set; } = null!;
        public uint minFeeA { get; set; }
        public uint minFeeB { get; set; }
        public uint coinsPerWordUTXO { get; set; }

        // Current Slot
        public Blockfrost.Api.Models.BlockContentResponse tip { get; set; } = null!;
        public uint currentSlot { get; set; }

        public BlockfrostData() { }

        public async Task UpdateData(
            IBlocksService blockfrostBlocksService,
            IEpochsService blockfrostEpochsService
        )
        {
            // Protocol Parameters
            this.protocolParameters = await blockfrostEpochsService.GetLatestParametersAsync();
            this.minFeeA = (uint)protocolParameters.MinFeeA;
            this.minFeeB = (uint)protocolParameters.MinFeeB;
            this.coinsPerWordUTXO = uint.Parse(protocolParameters.CoinsPerUtxoWord);

            // Current Slot
            this.tip = await blockfrostBlocksService.GetLatestAsync();
            this.currentSlot = (uint)tip.Slot!;
        }

        public class AddressAssetUtxoContentResponse
        {
            public AddressAssetUtxoContentResponse() { }

            public string address { get; set; } = default!;
            public string tx_hash { get; set; } = default!;
            public long tx_index { get; set; } = default!;
            public long output_index { get; set; } = default!;
            public object amount { get; set; } = default!;
            public string block { get; set; } = default!;
            public string data_hash { get; set; } = default!;
            public string inline_datum { get; set; } = default!;
            public string reference_script_hash { get; set; } = default!;
        }
    }

    public async Task<Utxo> GetAssetUTXO(string address, string asset)
    {
        try
        {
            // string address = tokenData.referenceScriptAddress!;
            // string asset = tokenData.referenceAssetFullNameHex;

            int pageNumber = 1;
            int countPerPage = 100;
            ESortOrder sortOrder = ESortOrder.Desc;
            List<AddressAssetUtxoContentResponse> blockfrostAddressUTXOs =
                await GetAddressAssetUTXOs(address, asset, countPerPage, pageNumber, sortOrder);
            AddressAssetUtxoContentResponse addressAssetUTXO = blockfrostAddressUTXOs.First();

            string txHash = addressAssetUTXO.tx_hash;
            uint txIndex = (uint)addressAssetUTXO.tx_index;
            string outputAddress = address;

            var amountObjects = JsonSerializer.Serialize(addressAssetUTXO.amount);
            List<Amount> amounts = JsonSerializer.Deserialize<List<Amount>>(amountObjects)!;
            Balance balance = GetBalance(amounts);

            Utxo utxo = new Utxo
            {
                TxHash = txHash,
                TxIndex = txIndex,
                OutputAddress = outputAddress,
                Balance = balance
            };
            return utxo;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.ToString());
        }

        return null!;
    }

    public async Task<List<Utxo>> GetUTXOs(List<string> addresses)
    {
        List<Utxo> utxos = new List<Utxo>();
        foreach (string accountAddress in addresses)
        {
            try
            {
                int pageNumber = 1;
                int countPerPage = 100;
                ESortOrder sortOrder = ESortOrder.Desc;
                AddressUtxoContentResponseCollection blockfrostAddressUTXOs;
                do
                {
                    blockfrostAddressUTXOs = await blockfrostAddressesService.GetUtxosAsync(
                        accountAddress,
                        countPerPage,
                        pageNumber,
                        sortOrder
                    );
                    foreach (var blockfrostAddressUTXO in blockfrostAddressUTXOs)
                    {
                        string txHash = blockfrostAddressUTXO.TxHash;
                        uint txIndex = (uint)blockfrostAddressUTXO.TxIndex;
                        string outputAddress = accountAddress;

                        var amountObjects = JsonSerializer.Serialize(blockfrostAddressUTXO.Amount);
                        List<Amount> amounts = JsonSerializer.Deserialize<List<Amount>>(
                            amountObjects
                        )!;
                        Balance balance = GetBalance(amounts);

                        Utxo utxo = new Utxo
                        {
                            TxHash = txHash,
                            TxIndex = txIndex,
                            OutputAddress = outputAddress,
                            Balance = balance
                        };
                        utxos.Add(utxo);
                    }

                    pageNumber += 1;
                } while (blockfrostAddressUTXOs?.Count == countPerPage);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        return utxos;
    }

    public Balance GetBalance(List<Amount> amounts)
    {
        ulong lovelaces = 0;
        List<Asset> assets = new List<Asset>();
        foreach (Amount amount in amounts)
        {
            if (amount.Unit == "lovelace")
            {
                lovelaces = ulong.Parse(amount.Quantity);
            }
            else
            {
                Asset asset = new Asset();
                asset.PolicyId = amount.Unit[..56];
                asset.Name = amount.Unit[56..];
                asset.Quantity = long.Parse(amount.Quantity);
                assets.Add(asset);
            }
        }

        return new Balance() { Lovelaces = lovelaces, Assets = assets };
    }

    //---------------------------------------------------------------------------------------------------//
    // Block API Functions Not in SDK
    //---------------------------------------------------------------------------------------------------//

    public async Task<object> GetProtocolParameters()
    {
        string uri = "https://cardano-mainnet.blockfrost.io";
        if (this.cardanoNetwork == "preprod")
        {
            uri = "https://cardano-preprod.blockfrost.io";
        }
        string endpoint = $"/api/v0/epochs/latest/parameters";

        RequestData requestData = new RequestData
        {
            uri = uri,
            endpoint = endpoint,
            httpMethod = HttpMethod.Get,
            headers = new Dictionary<string, string> { { "project_id", this.blockfrostAPIKey } },
            contentType = "application/json"
        };

        HttpResponseMessage response = await APIService.SendRequest(requestData);
        object protocolParameters = await APIService.Content<object>(response);
        return protocolParameters;
    }

    public async Task<EvaluatedTransaction> EvaluateScriptTransaction(byte[] bytes)
    {
        string uri = "https://cardano-mainnet.blockfrost.io";
        if (this.cardanoNetwork == "preprod")
        {
            uri = "https://cardano-preprod.blockfrost.io";
        }
        string endpoint = $"/api/v0/utils/txs/evaluate";

        RequestData requestData = new RequestData
        {
            uri = uri,
            endpoint = endpoint,
            body = Convert.ToBase64String(bytes),
            httpMethod = HttpMethod.Post,
            headers = new Dictionary<string, string> { { "project_id", this.blockfrostAPIKey } },
            contentType = "application/cbor"
        };

        HttpResponseMessage response = await APIService.SendRequest(requestData);

        // If we are missing data from a Cardano CLI, update this object class
        EvaluatedTransaction evaluate = await APIService.Content<EvaluatedTransaction>(response);
        return evaluate;
    }

    public class EvaluatedTransaction
    {
        public string? type { get; set; } = default!;
        public string? version { get; set; } = default!;
        public string? servicename { get; set; } = default!;
        public string? methodname { get; set; } = default!;
        public EvaluationObjectResult? result { get; set; } = default!;

        public Dictionary<string, ExUnits> GetExUnits()
        {
            Dictionary<string, ExUnits> exUnits = new Dictionary<string, ExUnits>();
            if (result == null || result.EvaluationResult == null)
                return null!;

            foreach (var evaluationResult in result.EvaluationResult!)
            {
                ExUnits exUnit = new ExUnits
                {
                    Mem = (ulong)evaluationResult.Value.memory!,
                    Steps = (ulong)evaluationResult.Value.steps!
                };
                exUnits.Add(evaluationResult.Key, exUnit);
            }
            return exUnits;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class EvaluationObjectResult
    {
        public Dictionary<string, EvaluationExUnits>? EvaluationResult { get; set; } =
            new Dictionary<string, EvaluationExUnits>()!;
        public EvaluationFailure EvaluationFailure { get; set; } = default!;
    }

    public class EvaluationFailure
    {
        public Dictionary<string, List<ScriptFailure>> ScriptFailures { get; set; } = default!;
    }

    public class ScriptFailure
    {
        public ValidatorFailure validatorFailed { get; set; } = default!;
    }

    public class ValidatorFailure
    {
        public string? error { get; set; } = "";
        public List<string>? traces { get; set; } = new List<string>();
    }

    public class EvaluationExUnits
    {
        public ulong? memory { get; set; } = default!;
        public ulong? steps { get; set; } = default!;
    }
    //---------------------------------------------------------------------------------------------------//
}
