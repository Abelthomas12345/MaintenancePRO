# MaintenancePro 🛠️ (Version multiplateforme : Android + Windows — .NET MAUI)

**Examen pratique — Atelier de Développement Mobile**
*Niveau 5 Génie Logiciel | Année académique 2026/2027*
**Étudiant :** NDOUNGUE ESSOH ABEL THOMAS
**Enseignant :** M. Dada Nelson

---

## 📌 Présentation

Dans le secteur industriel, les pannes et les interventions sont souvent consignées sur papier, ce qui provoque des pertes d'informations et empêche de disposer d'un historique exploitable.

**MaintenancePro** numérise ce processus. Cette version regroupe en **un seul projet .NET MAUI** :

- une **application Android** (émulateur ou téléphone) ;
- une **application Windows** (exécutable de bureau).

Les deux plateformes partagent le même code (vues, ViewModels, modèles, services) et la même interface, inspirée des maquettes Figma : fond bleu nuit, panneaux ardoise, accent orange.

---

## 🏬 Domaine et problématique

- **Domaine :** Industrie et maintenance
- **Problème :** consignation manuelle des pannes et interventions, historique inutilisable
- **Objectif :** enregistrer les interventions, centraliser le suivi et constituer un historique par équipement, sur mobile comme sur poste fixe

---

## 🏗️ Architecture (MVVM)

```
MaintenancePRO/
├── Models/        Entités métier (Equipment, MaintenanceRecord)
├── Views/         Pages XAML (connexion, accueil, équipements, panne, diagnostic,
│                  intervention, historique, rapports, techniciens, paramètres)
├── ViewModels/    Logique de présentation et commandes
├── Services/      DemoDataService (données de démonstration)
├── Resources/     Icône et écran de démarrage
├── Platforms/
│   ├── Android/   MainActivity, MainApplication, AndroidManifest.xml
│   └── Windows/   Point d'entrée WinUI (App.xaml, manifestes)
├── AppShell.xaml  Navigation (Shell + menu latéral)
└── MaintenancePRO.csproj
```

Le dossier `Platforms/Windows` ne doit pas être supprimé (erreur `CS5001` sinon).

---

## ✨ Fonctionnalités

1. **Connexion** (mode démonstration)
2. **Accueil / tableau de bord** : équipements suivis, dossiers actifs, dernières interventions
3. **Équipements** et **fiche équipement**
4. **Déclaration de panne**
5. **Diagnostic**
6. **Intervention** et **détail d'intervention**
7. **Historique par machine**
8. **Rapports**, **techniciens**, **paramètres**

---

## 🚀 Prérequis

| | Windows | Android |
|---|---|---|
| Système | Windows 10 (1809) ou 11, 64 bits | Android 7.0 (API 24) minimum |
| Outils | Visual Studio compatible .NET 10 | Visual Studio + Android SDK |
| Appareil | « Windows Machine » | Émulateur (Device Manager) ou téléphone en débogage USB |

- **SDK :** .NET 10
- **Workload Visual Studio :** « Développement d'interface utilisateur d'applications .NET multiplateformes » (.NET MAUI), avec les composants Android et Windows

---

## ▶️ Installation et exécution

1. Cloner ou décompresser le projet.
2. Ouvrir `MaintenancePRO.csproj` dans Visual Studio. Les packages NuGet se restaurent automatiquement (ou `dotnet restore`).
3. Choisir la cible dans la barre d'outils :
   - **Windows Machine** → l'application s'ouvre dans une fenêtre ;
   - **un émulateur Android** ou **un téléphone connecté** → l'application s'installe sur l'appareil.
4. Lancer avec **F5**.

### En ligne de commande

```bash
dotnet build -f net10.0-android
dotnet build -f net10.0-windows10.0.19041.0
```

Les cibles de compilation sont `net10.0-android` (toujours) et `net10.0-windows10.0.19041.0` (ajoutée automatiquement sous Windows).

---

## 📦 Dépendances

- **Windows :** `Microsoft.Maui.Controls` 10.0.20 et `Microsoft.Extensions.Logging.Debug` 10.0.0
- **Android :** aucun package NuGet, uniquement le workload MAUI

---

## ⚠️ Limites actuelles

Prototype front-end :

- données de démonstration locales, sans API ni base de données ;
- authentification simulée ;
- l'ajout de photos et les rapports calculés ne sont pas encore implémentés ;
- les formulaires valident localement et n'enregistrent rien sur un serveur.

Évolution prévue : remplacer `DemoDataService` par des appels à une API et ajouter la persistance.

---

## 🔧 Dépannage

| Problème | Piste |
|---|---|
| `NU1301` / `NU1900` | Vérifier l'accès à NuGet ou au cache local (`NU1900` n'est pas forcément bloquant) |
| `MA002` | Vérifier la restauration de `Microsoft.Maui.Controls` (cible Windows) |
| `CS5001` | Le dossier `Platforms/Windows` est manquant |
| Aucun émulateur listé | Le créer et le démarrer dans Device Manager, ou activer le débogage USB |
