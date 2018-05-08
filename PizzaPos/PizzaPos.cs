using System;
using SPIClient;

namespace PizzaPos
{
    internal class PizzaPos
    {
        private static void Main(string[] args)
        {
            var myPos = new PizzaPos();
            myPos.Start();
        }

        private Spi _spi;
        private string _posId = "";
        private string _eftposAddress = "";
        private Secrets _spiSecrets = null;
        
        private void Start()
        {
            LoadPersistedState();
            
            _spi = new Spi(_posId, _eftposAddress, _spiSecrets); // It is ok to not have the secrets yet to start with.
            _spi.StatusChanged += OnSpiStatusChanged;
            _spi.PairingFlowStateChanged += OnPairingFlowStateChanged;
            _spi.SecretsChanged += OnSecretsChanged;
            _spi.TxFlowStateChanged += OnTxFlowStateChanged;
            _spi.Start();
            
            Console.Clear();
            Console.WriteLine("# Welcome to PizzaPos !");
            PrintStatusAndActions();
            Console.Write("> ");
            AcceptUserInput();
        }

        private void OnTxFlowStateChanged(object sender, TransactionFlowState txState)
        {
            Console.Clear();
            PrintStatusAndActions();
            Console.Write("> ");
        }

        private void OnPairingFlowStateChanged(object sender, PairingFlowState pairingFlowState)
        {
            Console.Clear();
            PrintStatusAndActions();
            Console.Write("> ");
        }

        private void OnSecretsChanged(object sender, Secrets secrets)
        {
            _spiSecrets = secrets;
            Console.WriteLine(secrets != null
                ? $"# I Have Secrets: {secrets.EncKey}{secrets.HmacKey}. Persist them Securely."
                : $"# I Have Lost the Secrets, i.e. Unpaired. Destroy the persisted secrets.");
        }
        
        private void OnSpiStatusChanged(object sender, SpiStatusEventArgs status)
        {
            Console.Clear();
            Console.WriteLine($"# --> SPI Status Changed: {status.SpiStatus}");
            PrintStatusAndActions();
            Console.Write("> ");
        }

        private void PrintStatusAndActions()
        {
            PrintFlowInfo();

            PrintActions();

            PrintPairingStatus();
        }

        private void PrintFlowInfo()
        {
            if (_spi.CurrentFlow == SpiFlow.Pairing)
            {
                var pairingState = _spi.CurrentPairingFlowState;
                Console.WriteLine("### PAIRING PROCESS UPDATE ###");
                Console.WriteLine($"# {pairingState.Message}");
                Console.WriteLine($"# Finished? {pairingState.Finished}");
                Console.WriteLine($"# Successful? {pairingState.Successful}");
                Console.WriteLine($"# Confirmation Code: {pairingState.ConfirmationCode}");
                Console.WriteLine($"# Waiting Confirm from Eftpos? {pairingState.AwaitingCheckFromEftpos}");
                Console.WriteLine($"# Waiting Confirm from POS? {pairingState.AwaitingCheckFromPos}");
            }

            if (_spi.CurrentFlow == SpiFlow.Transaction)
            {
                var txState = _spi.CurrentTxFlowState;
                Console.WriteLine("### TX PROCESS UPDATE ###");
                Console.WriteLine($"# {txState.DisplayMessage}");
                Console.WriteLine($"# Id: {txState.Id}");
                Console.WriteLine($"# Type: {txState.Type}");
                Console.WriteLine($"# Amount: ${txState.AmountCents / 100.0}");
                Console.WriteLine($"# Waiting For Signature: {txState.AwaitingSignatureCheck}");
                Console.WriteLine($"# Attempting to Cancel : {txState.AttemptingToCancel}");
                Console.WriteLine($"# Finished: {txState.Finished}");
                Console.WriteLine($"# Success: {txState.Success}");
                
                if (txState.Finished)
                {
                    Console.WriteLine($"");
                    switch (txState.Success)
                    {
                        case Message.SuccessState.Success:
                            if (txState.Type == TransactionType.Purchase)
                            {
                                Console.WriteLine($"# WOOHOO - WE GOT PAID!");
                                var purchaseResponse = new PurchaseResponse(txState.Response);
                                Console.WriteLine("# Response: {0}", purchaseResponse.GetResponseText());
                                Console.WriteLine("# RRN: {0}", purchaseResponse.GetRRN());
                                Console.WriteLine("# Scheme: {0}", purchaseResponse.SchemeName);
                                Console.WriteLine($"# Settlement Date:{purchaseResponse.GetSettlementDate()}");
                                Console.WriteLine("# Customer Receipt:");
                                Console.WriteLine(purchaseResponse.GetCustomerReceipt().TrimEnd());
                            }
                            else if (txState.Type == TransactionType.Refund)
                            {
                                Console.WriteLine($"# REFUND GIVEN - OH WELL!");
                                var refundResponse = new RefundResponse(txState.Response);
                                Console.WriteLine("# Response: {0}", refundResponse.GetResponseText());
                                Console.WriteLine("# RRN: {0}", refundResponse.GetRRN());
                                Console.WriteLine("# Scheme: {0}", refundResponse.SchemeName);
                                Console.WriteLine($"# Settlement Date:{refundResponse.GetSettlementDate()}");
                                Console.WriteLine("# Customer Receipt:");
                                Console.WriteLine(refundResponse.GetCustomerReceipt().TrimEnd());                                
                            }
                            else if (txState.Type == TransactionType.Settle)
                            {
                                Console.WriteLine($"# SETTLEMENT SUCCESSFUL!");
                                if (txState.Response != null)
                                {
                                    var settleResponse = new Settlement(txState.Response);
                                    Console.WriteLine("# Response: {0}", settleResponse.GetResponseText());
                                    Console.WriteLine("# Merchant Receipt:");
                                    Console.WriteLine(settleResponse.GetReceipt().TrimEnd());
                                }
                            }
                            break;
                        case Message.SuccessState.Failed:
                            if (txState.Type == TransactionType.Purchase)
                            {
                                Console.WriteLine($"# WE DID NOT GET PAID :(");
                                if (txState.Response != null)
                                {
                                    var purchaseResponse = new PurchaseResponse(txState.Response);
                                    Console.WriteLine("# Error: {0}", txState.Response.GetError());
                                    Console.WriteLine("# Response: {0}", purchaseResponse.GetResponseText());
                                    Console.WriteLine("# RRN: {0}", purchaseResponse.GetRRN());
                                    Console.WriteLine("# Scheme: {0}", purchaseResponse.SchemeName);
                                    Console.WriteLine("# Customer Receipt:");
                                    Console.WriteLine(purchaseResponse.GetCustomerReceipt().TrimEnd());
                                }
                            }
                            else if (txState.Type == TransactionType.Refund)
                            {
                                Console.WriteLine($"# REFUND FAILED!");
                                if (txState.Response != null)
                                {
                                    var refundResponse = new RefundResponse(txState.Response);
                                    Console.WriteLine("# Response: {0}", refundResponse.GetResponseText());
                                    Console.WriteLine("# RRN: {0}", refundResponse.GetRRN());
                                    Console.WriteLine("# Scheme: {0}", refundResponse.SchemeName);
                                    Console.WriteLine("# Customer Receipt:");
                                    Console.WriteLine(refundResponse.GetCustomerReceipt().TrimEnd());
                                }
                            }
                            else if (txState.Type == TransactionType.Settle)
                            {
                                Console.WriteLine($"# SETTLEMENT FAILED!");
                                if (txState.Response != null)
                                {
                                    var settleResponse = new Settlement(txState.Response);
                                    Console.WriteLine("# Response: {0}", settleResponse.GetResponseText());
                                    Console.WriteLine("# Error: {0}", txState.Response.GetError());
                                    Console.WriteLine("# Merchant Receipt:");
                                    Console.WriteLine(settleResponse.GetReceipt().TrimEnd());
                                }
                            }

                            break;
                        case Message.SuccessState.Unknown:
                            if (txState.Type == TransactionType.Purchase)
                            {
                                Console.WriteLine($"# WE'RE NOT QUITE SURE WHETHER WE GOT PAID OR NOT :/");
                                Console.WriteLine($"# CHECK THE LAST TRANSACTION ON THE EFTPOS ITSELF FROM THE APPROPRIATE MENU ITEM.");
                                Console.WriteLine($"# IF YOU CONFIRM THAT THE CUSTOMER PAID, CLOSE THE ORDER.");
                                Console.WriteLine($"# OTHERWISE, RETRY THE PAYMENT FROM SCRATCH.");
                            }
                            else if (txState.Type == TransactionType.Refund)
                            {
                                Console.WriteLine($"# WE'RE NOT QUITE SURE WHETHER THE REFUND WENT THROUGH OR NOT :/");
                                Console.WriteLine($"# CHECK THE LAST TRANSACTION ON THE EFTPOS ITSELF FROM THE APPROPRIATE MENU ITEM.");
                                Console.WriteLine($"# YOU CAN THE TAKE THE APPROPRIATE ACTION.");
                            }
                            break;
                    }
                }
            }
            Console.WriteLine("");
        }

        private void PrintActions()
        {
            Console.WriteLine("# ----------- AVAILABLE ACTIONS ------------");

            if (_spi.CurrentFlow == SpiFlow.Idle)
            {
                Console.WriteLine("# [pizza:funghi] - charge for a pizza!");
                Console.WriteLine("# [yuck] - hand out a refund!");
                Console.WriteLine("# [settle] - Initiate Settlement");
            }

            if (_spi.CurrentStatus == SpiStatus.Unpaired && _spi.CurrentFlow == SpiFlow.Idle)
            {
                Console.WriteLine("# [pos_id:CITYPIZZA1] - Set the POS ID");
                Console.WriteLine("# [eftpos_address:10.161.104.104] - Set the EFTPOS ADDRESS");
            }

            if (_spi.CurrentStatus == SpiStatus.Unpaired && _spi.CurrentFlow == SpiFlow.Idle)
                Console.WriteLine("# [pair] - Pair with Eftpos");

            if (_spi.CurrentStatus != SpiStatus.Unpaired && _spi.CurrentFlow == SpiFlow.Idle)
                Console.WriteLine("# [unpair] - Unpair and Disconnect");
            
            if (_spi.CurrentFlow == SpiFlow.Pairing)
            {
                Console.WriteLine("# [pair_cancel] - Cancel Pairing");

                if (_spi.CurrentPairingFlowState.AwaitingCheckFromPos)
                    Console.WriteLine("# [pair_confirm] - Confirm Pairing Code");

                if (_spi.CurrentPairingFlowState.Finished)
                    Console.WriteLine("# [ok] - acknowledge final");
            }

            if (_spi.CurrentFlow == SpiFlow.Transaction)
            {
                var txState = _spi.CurrentTxFlowState;

                if (txState.AwaitingSignatureCheck)
                {
                    Console.WriteLine("# [tx_sign_accept] - Accept Signature");
                    Console.WriteLine("# [tx_sign_decline] - Decline Signature");
                }

                if (!txState.Finished && !txState.AttemptingToCancel)
                    Console.WriteLine("# [tx_cancel] - Attempt to Cancel Tx");

                if (txState.Finished)
                    Console.WriteLine("# [ok] - acknowledge final");
            }
            
            Console.WriteLine("# [status] - reprint buttons/status");
            Console.WriteLine("# [bye] - exit");
            Console.WriteLine();
        }

        private void PrintPairingStatus()
        {
            Console.WriteLine("# --------------- STATUS ------------------");
            Console.WriteLine($"# {_posId} <-> Eftpos: {_eftposAddress} #");
            Console.WriteLine($"# SPI STATUS: {_spi.CurrentStatus}     FLOW: {_spi.CurrentFlow} #");
            Console.WriteLine("# CASH ONLY! #");
            Console.WriteLine("# -----------------------------------------");
        }
        
        private void AcceptUserInput()
        {
            var bye = false;
            while (!bye)
            {
                var input = Console.ReadLine();
                if (input == null) continue;
                var spInput = input.Split(':');
                switch (spInput[0])
                {
                    case "pizza":
                        var pres = _spi.InitiatePurchaseTx(RequestIdHelper.Id("pizza"), 1000);
                        if (!pres.Initiated)
                        {
                            Console.WriteLine($"# Could not initiate purchase: {pres.Message}. Please Retry.");
                        }
                        break;
                    case "yuck":
                        var yuckres = _spi.InitiateRefundTx(RequestIdHelper.Id("yuck"), 1000);
                        if (!yuckres.Initiated)
                        {
                            Console.WriteLine($"# Could not initiate refund: {yuckres.Message}. Please Retry.");
                        }
                        break;

                    case "pos_id":
                        Console.Clear();
                        if (_spi.SetPosId(spInput[1]))
                        {
                            _posId = spInput[1];
                            Console.WriteLine($"## -> POS ID now set to {_posId}");
                        }
                        else
                        {
                            Console.WriteLine($"## -> Could not set POS ID");
                        }
                        PrintStatusAndActions();
                        Console.Write("> ");
                        break;
                    case "eftpos_address":
                        Console.Clear();
                        if (_spi.SetEftposAddress(spInput[1]))
                        {
                            _eftposAddress = spInput[1];
                            Console.WriteLine($"## -> Eftpos Address now set to {_eftposAddress}");
                        }
                        else
                        {
                            Console.WriteLine($"## -> Could not set Eftpos Address");
                        }
                        PrintStatusAndActions();
                        Console.Write("> ");
                        break;
                        
                    case "pair":
                        _spi.Pair();
                        break;
                    case "pair_cancel":
                        _spi.PairingCancel();
                        break;
                    case "pair_confirm":
                        _spi.PairingConfirmCode();
                        break;
                    case "unpair":
                        _spi.Unpair();
                        break;

                    case "tx_sign_accept":
                        _spi.AcceptSignature(true);
                        break;
                    case "tx_sign_decline":
                        _spi.AcceptSignature(false);
                        break;
                    case "tx_cancel":
                        _spi.CancelTransaction();
                        break;
                    
                    case "settle":
                        var settleres = _spi.InitiateSettleTx(RequestIdHelper.Id("settle"));
                        if (!settleres.Initiated)
                        {
                            Console.WriteLine($"# Could not initiate settlement: {settleres.Message}. Please Retry.");
                        }
                        break;
                        
                    case "ok":
                        Console.Clear();
                        _spi.AckFlowEndedAndBackToIdle();
                        PrintStatusAndActions();
                        Console.Write("> ");
                        break;
                        
                    case "status":
                        Console.Clear();
                        PrintStatusAndActions();
                        break;
                    case "bye":
                        bye = true;
                        break;
                    case "":
                        Console.Write("> ");
                        break;
                        
                    default:
                        Console.WriteLine("# I don't understand. Sorry.");
                        break;
                }
            }
            Console.WriteLine("# BaBye!");
            if (_spiSecrets != null)
                Console.WriteLine($"{_posId}:{_eftposAddress}:{_spiSecrets.EncKey}:{_spiSecrets.HmacKey}");
        }
        
        private void LoadPersistedState()
        {
            // Let's read cmd line arguments.
            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs.Length <= 1) return; // nothing passed in

            if (string.IsNullOrWhiteSpace(cmdArgs[1])) return;
            
            var argSplit = cmdArgs[1].Split(':');
            _posId = argSplit[0];
            _eftposAddress = argSplit[1];
            _spiSecrets = new Secrets(argSplit[2], argSplit[3]);
        }
        
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger("spi");
    }
}