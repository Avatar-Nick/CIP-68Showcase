using CardanoSharp.Wallet;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.Utilities;

public class Wallet
{
    public string? mnemonicWords { get; set; }
    public string? address { get; set; }
    public PublicKey publicKey { get; set; } = default!;
    public PrivateKey privateKey { get; set; } = default!;

    public Wallet(string? newWords = null)
    {
        if (newWords != null)
            this.mnemonicWords = newWords;

        GetWallet();
    }

    public void GenerateWallet()
    {
        IMnemonicService service = new MnemonicService();

        int wordCount = 24;
        Mnemonic mnemonic = service.Generate(wordCount, WordLists.English);
        Console.WriteLine(mnemonic.Words);

        int accountIx = 0;
        int keyIx = 0;

        // Derive Payment Keys
        var accountNode = mnemonic
            .GetMasterNode("test")
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(accountIx);
        var paymentNode = accountNode.Derive(RoleType.ExternalChain).Derive(keyIx);
        var stakingNode = accountNode.Derive(RoleType.Staking).Derive(keyIx);

        PrivateKey accountPrv = accountNode.PrivateKey;
        PublicKey accountPub = accountNode.PublicKey;

        PrivateKey paymentPrv = paymentNode.PrivateKey;
        PublicKey paymentPub = paymentNode.PublicKey;

        PrivateKey stakePrv = stakingNode.PrivateKey;
        PublicKey stakePub = stakingNode.PublicKey;

        Address baseAddr = AddressUtility.GetBaseAddress(accountPub, stakePub, NetworkType.Preprod);

        // Set Values
        this.address = baseAddr.ToString();
        this.privateKey = accountPrv;
        this.publicKey = accountPub;
        Console.WriteLine(baseAddr.ToString());
    }

    public void GetWallet()
    {
        IMnemonicService service = new MnemonicService();
        Mnemonic mnemonic = service.Restore(mnemonicWords!);

        int accountIx = 0;
        int keyIx = 0;

        // Derive Payment Keys
        var accountNode = mnemonic
            .GetMasterNode("test")
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(accountIx);
        var paymentNode = accountNode.Derive(RoleType.ExternalChain).Derive(keyIx);
        var stakingNode = accountNode.Derive(RoleType.Staking).Derive(keyIx);

        PrivateKey accountPrv = accountNode.PrivateKey;
        PublicKey accountPub = accountNode.PublicKey;

        PrivateKey paymentPrv = paymentNode.PrivateKey;
        PublicKey paymentPub = paymentNode.PublicKey;

        PrivateKey stakePrv = stakingNode.PrivateKey;
        PublicKey stakePub = stakingNode.PublicKey;

        Address baseAddr = AddressUtility.GetBaseAddress(accountPub, stakePub, NetworkType.Preprod);

        // Set Values
        this.address = baseAddr.ToString();
        this.privateKey = accountPrv;
        this.publicKey = accountPub;
    }
}
