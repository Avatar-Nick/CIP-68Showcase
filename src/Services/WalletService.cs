using System.Text.Json;
using CardanoSharp.Wallet.Extensions;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness.NativeScripts;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;
using TestEnvironment.Setup;

public static class WalletService
{
    public static string BuildCborHexPayload(byte[] keyPayload)
    {
        // Key Payload is the .Key property of the keys (Don't use the ChainCode)
        return $"58{keyPayload.Length:x2}{keyPayload.ToStringHex()}";
    }

    public static byte[] BuildByteArrayFromCbor(string cborHex)
    {
        string hex = cborHex[4..];
        byte[] bytes = hex.HexToByteArray();
        return bytes;
    }

    public static PublicKey GetPublicKey(Wallet wallet)
    {
        return wallet.publicKey;
    }

    public static string GetPublicKeyHash(Wallet wallet)
    {
        PublicKey publicKey = GetPublicKey(wallet);
        return HashUtility.Blake2b224(publicKey.Key).ToStringHex();
    }

    public static PrivateKey GetPrivateKey(Wallet wallet)
    {
        return wallet.privateKey;
    }

    public static PublicKey GetPublicPaymentKey(Wallet wallet)
    {
        return wallet.publicKey;
    }

    public static PrivateKey GetPrivatePaymentKey(Wallet wallet)
    {
        return wallet.privateKey;
    }

    public static PublicKey GetPublicStakeKey(Wallet wallet)
    {
        return wallet.publicKey;
    }

    public static PrivateKey GetPrivateStakeKey(Wallet wallet)
    {
        return wallet.privateKey;
    }

    public static IScriptAllBuilder GetPolicyScriptBuilder(
        Wallet wallet,
        DateTime? invalidAfterDatetime
    )
    {
        PrivateKey privateKey = GetPrivateKey(wallet);
        PublicKey publicKey = GetPublicKey(wallet);

        KeyPair keypair = new KeyPair(privateKey, publicKey);

        var scriptBuilder = ScriptAllBuilder.Create;
        var policyKeyHash = HashUtility.Blake2b224(keypair.PublicKey.Key);

        var publicKeyHashNativeScriptBuilder = NativeScriptBuilder.Create.SetKeyHash(policyKeyHash);
        scriptBuilder.SetScript(publicKeyHashNativeScriptBuilder);

        // If invalid after datetime exists, add a new NativeScript to the ScriptAll with the SetScript function
        if (invalidAfterDatetime != null)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            long invalidAfterTime = Convert.ToInt64((invalidAfterDatetime - epoch)?.TotalSeconds);
            uint invalidAfterSlot = Convert.ToUInt32(
                CardanoService.GetSlotFromUnixTIme(invalidAfterTime)
            );
            var invalidAfterTimeNativeScriptBuilder = NativeScriptBuilder.Create.SetInvalidAfter(
                invalidAfterSlot
            );
            scriptBuilder.SetScript(invalidAfterTimeNativeScriptBuilder);
        }

        return scriptBuilder;
    }

    public static byte[] GetPolicyId(Wallet wallet, DateTime? invalidAfterDatetime)
    {
        IScriptAllBuilder scriptBuilder = GetPolicyScriptBuilder(wallet, invalidAfterDatetime);
        ScriptAll scriptAll = scriptBuilder.Build();
        byte[] policyId = scriptAll.GetPolicyId();
        return policyId;
    }
}
