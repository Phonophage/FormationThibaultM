﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Projet_Partie_2
{
    class Program
    {
        static void Main(string[] args)
        {
            List<Gestionnaire> gestionnaires = Entree.LireFichierGest("Gestionnaires.csv");
            List<Operation> operations = Entree.LireFichierComptes(gestionnaires, "Comptes.csv");
            List<Transaction> transactions = Entree.LireFichierTransactions("Transactions.csv");

            Traitement(gestionnaires, operations, transactions);

            Sortie.EcrireSortieOperations(operations, "Statut operations.csv");
            Sortie.EcrireSortieTransactions(transactions, "Statut transactions.csv");
            Sortie.EcrireSortieMetrologie(gestionnaires, "Métrologie.txt");
        }

        static void Traitement (List<Gestionnaire> gestionnaires, List<Operation> operations, List<Transaction> transactions)
        {
            int i_ope = 0;
            int i_tra = 0;

            while (i_ope < operations.Count() || i_tra < transactions.Count())
            {
                int dateCompare = DateTime.Compare(operations[i_ope].GetDate(), transactions[i_tra].GetDate());

                if (dateCompare <= 0) // opération avant transaction
                {
                    OperationCompte(operations[i_ope], gestionnaires);
                    i_ope++;
                }
                else // transaction avant opération
                {
                    TraiterTransaction(transactions[i_tra], gestionnaires);
                    i_tra++;
                }
            }
        }

        static bool OperationCompte(Operation ope, List<Gestionnaire> gestionnaires)
        {
            int gest_in = Outils.TrouverGest(gestionnaires, ope.GetEntree());
            int gest_out = Outils.TrouverGest(gestionnaires, ope.GetSortie());

            if (ope.GetEntree() != -1 && ope.GetSortie() == -1)                 // ouverture de compte
            {
                if (!Outils.CompteExiste(gestionnaires, ope.GetIdentifiant()))  // on vérifie que le compte n'existe pas déjà
                {
                    gestionnaires[gest_in].AddCompte(new Compte(ope.GetIdentifiant(), ope.GetDate(), ope.GetSolde()));
                    Compte.SetNombreComptes(Compte.GetNombreComptes() + 1);

                    return true;
                }

                return false;
            }
            else if (ope.GetEntree() == -1 && ope.GetSortie() != -1)            // fermeture de compte
            {
                if (Outils.CompteExiste(gestionnaires, ope.GetIdentifiant()))   // on vérifie que le compte existe bien
                {
                    gestionnaires[gest_out].CloseCompte(ope.GetIdentifiant(), ope.GetDate());
                    Compte.SetNombreComptes(Compte.GetNombreComptes() - 1);

                    return true;
                }

                return false;
            }
            else if (ope.GetEntree() != -1 && ope.GetSortie() != -1)                    // transfert de compte
            {
                if (Outils.GestExiste(gestionnaires, ope.GetEntree()) && Outils.GestExiste(gestionnaires, ope.GetSortie()) && gestionnaires[gest_in].CompteExiste(ope.GetIdentifiant()) &&
                    gestionnaires[gest_in].GetCompte(ope.GetIdentifiant()).IsActif())   // on vérifie l'existance des deux gestionnaires et du compte à transférer, et on vérifie que le compte est actif
                {
                    gestionnaires[gest_in].AddCompte(gestionnaires[gest_out].GetCompte(ope.GetIdentifiant()));
                    gestionnaires[gest_out].DelCompte(ope.GetIdentifiant());

                    return true;
                }
            }

            return false;
        }

        static bool TraiterTransaction(Transaction tran, List<Gestionnaire> gestionnaires)
        {
            if (tran.IsDoublon())
            {
                return false;
            }
            else if (tran.GetDestinataire() == 0 && tran.GetExpediteur() != 0)  // dépot
            {
                return TraiterDepot(tran, gestionnaires);
            }
            else if (tran.GetDestinataire() != 0 && tran.GetExpediteur() == 0)  // retrait
            {
                return TraiterRetrait(tran, gestionnaires);
            }
            else if (tran.GetDestinataire() != 0 && tran.GetExpediteur() != 0)  // virement
            {
                return TraiterVirement(tran, gestionnaires);
            }
            else                                                                // virement de l'environnement à l'environnement -> invalide
            {
                return false;
            }
        }

        static bool TraiterDepot(Transaction tran, List<Gestionnaire> gestionnaires)
        {
            if (tran.GetMontant() > 0)
            {
                int gest_des = Outils.GestOfCompte(gestionnaires, tran.GetDestinataire());

                if (gest_des != -1)
                {
                    Compte destinataire = gestionnaires[gest_des].GetCompte(tran.GetDestinataire());

                    if (tran.DateIsOk(destinataire))
                    {
                        destinataire.SetSolde(destinataire.GetSolde() + tran.GetMontant());
                        destinataire.AddTransaction(tran);
                        Transaction.SetNombreTransactions(Transaction.GetNombreTransactions() + 1);
                        Transaction.SetNombreTransactionsOk(Transaction.GetNombreTransactionsOk() + 1);
                        Transaction.SetMontantTransactionsOk(Transaction.GetMontantTransactionsOk() + tran.GetMontant());

                        return true;
                    }
                }
            }

            Transaction.SetNombreTransactions(Transaction.GetNombreTransactions() + 1);
            Transaction.SetNombreTransactionsKo(Transaction.GetNombreTransactionsKo() + 1);
            return false;
        }

        static bool TraiterRetrait(Transaction tran, List<Gestionnaire> gestionnaires)
        {
            if (tran.GetMontant() > 0)
            {
                int gest_exp = Outils.GestOfCompte(gestionnaires, tran.GetExpediteur());

                if (gest_exp != -1)
                {
                    Compte expediteur = gestionnaires[gest_exp].GetCompte(tran.GetDestinataire());

                    if (tran.GetMontant() <= expediteur.GetSolde() && expediteur.TransactionIsValid(tran) && tran.DateIsOk(expediteur))
                    {
                        expediteur.SetSolde(expediteur.GetSolde() - tran.GetMontant());
                        expediteur.AddTransaction(tran);
                        Transaction.SetNombreTransactions(Transaction.GetNombreTransactions() + 1);
                        Transaction.SetNombreTransactionsOk(Transaction.GetNombreTransactionsOk() + 1);
                        Transaction.SetMontantTransactionsOk(Transaction.GetMontantTransactionsOk() + tran.GetMontant());

                        return true;
                    }
                }
            }

            Transaction.SetNombreTransactions(Transaction.GetNombreTransactions() + 1);
            Transaction.SetNombreTransactionsKo(Transaction.GetNombreTransactionsKo() + 1);

            return false;
        }

        static bool TraiterVirement(Transaction tran, List<Gestionnaire> gestionnaires)
        {
            if (tran.GetMontant() > 0)
            {
                int gest_exp = Outils.GestOfCompte(gestionnaires, tran.GetExpediteur());
                int gest_des = Outils.GestOfCompte(gestionnaires, tran.GetDestinataire());

                if (gest_exp != -1 && gest_des != -1)
                {
                    Compte expediteur = gestionnaires[gest_exp].GetCompte(tran.GetExpediteur());
                    Compte destinataire = gestionnaires[gest_des].GetCompte(tran.GetDestinataire());

                    if (tran.GetMontant() <= expediteur.GetSolde() && expediteur.TransactionIsValid(tran) && tran.DateIsOk(expediteur))
                    {
                        expediteur.SetSolde(expediteur.GetSolde() - tran.GetMontant());

                        if (gest_exp == gest_des)   // virement entre deux comptes d'un même gestionnaire -> pas de frais de gestion
                        {
                            destinataire.SetSolde(expediteur.GetSolde() + tran.GetMontant());
                        }
                        else                        // frais de gestion
                        {
                            double frais_gestion = gestionnaires[gest_exp].FraisGestion(tran.GetMontant());
                            destinataire.SetSolde(expediteur.GetSolde() + tran.GetMontant() - frais_gestion);
                            gestionnaires[gest_exp].AddFraisGestion(frais_gestion);
                        }

                        expediteur.AddTransaction(tran);
                        destinataire.AddTransaction(tran);
                        Transaction.SetNombreTransactions(Transaction.GetNombreTransactions() + 1);
                        Transaction.SetNombreTransactionsOk(Transaction.GetNombreTransactionsOk() + 1);
                        Transaction.SetMontantTransactionsOk(Transaction.GetMontantTransactionsOk() + tran.GetMontant());

                        return true;
                    }
                }
            }

            Transaction.SetNombreTransactions(Transaction.GetNombreTransactions() + 1);
            Transaction.SetNombreTransactionsKo(Transaction.GetNombreTransactionsKo() + 1);

            return false;
        }
    }
}
