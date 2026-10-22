# MaintenancePro 🛠️ (Version Windows - .NET MAUI)

**Examen Pratique - Atelier de Développement Mobile**  
*Niveau 5 Génie Logiciel | Année Académique 2026/2027*  
**Étudiant :** NDOUNGUE ESSOH ABEL THOMAS  
**Enseignant :** M. Dada Nelson  

---

## 📌 Présentation du Projet

Dans le secteur industriel, la gestion traditionnelle des pannes et des interventions s'effectue fréquemment sur papier, entraînant des pertes d'informations et l'absence d'un historique exploitable. 

**MaintenancePro** est une solution logicielle moderne conçue pour numériser le processus de maintenance. Cette version spécifique cible l'environnement **Windows** grâce au framework **.NET MAUI**, offrant une interface ergonomique inspirée des maquettes Figma pour les techniciens et gestionnaires d'atelier.

---

## 🏬 Domaine & Problématique

* **Domaine :** Industrie & Maintenance
* **Problème traité :** Consignation manuelle sur papier des pannes et des interventions, rendant l'historique inutilisable et difficile d'accès.
* **Objectif principal :** Enregistrer les interventions de maintenance, centraliser le suivi et constituer un historique fiable par équipement sous une interface de bureau intuitive.

---

## 🏗️ Architecture Technique (.NET MAUI)

Le projet suit scrupuleusement le design pattern **MVVM (Model-View-ViewModel)** et s'articule autour de la structure de répertoires suivante :

* **`Models/`** : Contient les entités métier (ex: `Equipment.cs`, `MaintenanceRecord.cs`).
* **`Views/`** : Interfaces utilisateur en XAML (Pages de connexion, Tableau de bord, Fiches équipements, Diagnostics, Interventions, Historique, Rapports, Paramètres).
* **`ViewModels/`** : Logique de présentation et liaison des données (ex: `DashboardViewModel.cs`, `EquipmentViewModel.cs`, `DiagnosisViewModel.cs`, etc.).
* **`Services/`** : Services de gestion des données (ex: `DemoDataService.cs`).
* **`Platforms/Windows/`** : Spécificités de configuration et point d'entrée pour l'exécution sur le système d'exploitation Windows.

---

## ✨ Fonctionnalités Clés

1. **Tableau de Bord (`Dashboard`)** : Vue d'ensemble des équipements actifs, des pannes en cours et des interventions récentes.
2. **Fiche Équipement (`Equipment`)** : Consultation et gestion détaillée des caractéristiques des machines.
3. **Déclaration de Panne (`Breakdown`)** : Signalement rapide d'un dysfonctionnement industriel.
4. **Diagnostic (`Diagnosis`)** : Évaluation technique et identification de la cause racine.
5. **Gestion des Interventions (`Intervention`)** : Suivi des actions correctives, ajout de photos et détails des réparations.
6. **Historique par Machine (`History`)** : Traçabilité complète des interventions passées pour chaque équipement.
7. **Gestion des Techniciens & Rapports** : Suivi des intervenants et génération de bilans d'activité.

---

## 🚀 Prérequis et Installation (Environnement Windows)

### Prérequis logiciels
* **Système d'exploitation :** Windows 10 / 11 (64-bit)
* **IDE :** Visual Studio 2022 (version recommandée) ou Visual Studio Code avec les extensions .NET.
* **Charges de travail Visual Studio requises :**
  * Développement d'applications mobiles avec .NET (.NET MAUI)
  * Développement desktop .NET (si requis)
* **SDK .NET :** .NET 8.0 ou supérieur.

### Étapes d'installation et d'exécution

1. **Ouvrir le projet :**
   Lancez Visual Studio 2022 et sélectionnez *Ouvrir un projet ou une solution*, puis choisissez le fichier de projet `MaintenancePRO.csproj`.

2. **Restaurer les packages NuGet :**
   Les dépendances du projet se restaureront automatiquement à l'ouverture. Si nécessaire, exécutez dans le terminal :
   ```bash
   dotnet restore