# MaintenancePRO — version multiplateforme (Android + Windows)

Un seul projet .NET MAUI 10 qui produit :
- une **application Android** (émulateur Android Studio / Device Manager ou téléphone réel) ;
- une **application Windows** (exécutable, lancée via « Windows Machine »).

Les interfaces sont celles de la charte sombre (fond bleu nuit, accent orange). Les vues, ViewModels, modèles et le service de démonstration sont communs aux deux plateformes.

## Ouvrir et lancer
1. Décompresser l'archive et ouvrir `MaintenancePRO.csproj` dans Visual Studio (workload « Développement d'interface utilisateur d'applications .NET multiplateformes »).
2. Dans la barre d'outils, choisir la cible de lancement :
   - **Windows Machine** → l'app s'ouvre dans une fenêtre Windows ;
   - **un émulateur Android** (créé dans Device Manager) ou **un téléphone** en débogage USB → l'app s'installe sur l'appareil.
3. F5.

## Cibles de compilation
- `net10.0-android` : toujours présente.
- `net10.0-windows10.0.19041.0` : ajoutée automatiquement quand on compile sous Windows (ignorée sous macOS/Linux).

Pour compiler une seule cible en ligne de commande :
```
dotnet build -f net10.0-android
dotnet build -f net10.0-windows10.0.19041.0
```

## Dépendances
- Windows : `Microsoft.Maui.Controls` 10.0.20 et `Microsoft.Extensions.Logging.Debug` 10.0.0 (restaurés par NuGet).
- Android : aucun package NuGet, uniquement le workload MAUI.
Les deux configurations sont celles des deux projets d'origine.

## Structure
- `Views/`, `ViewModels/`, `Models/`, `Services/` : code partagé.
- `Platforms/Android/` : `MainActivity`, `MainApplication`, manifeste Android.
- `Platforms/Windows/` : point d'entrée WinUI (`App.xaml`, `App.xaml.cs`, manifestes). **Ne pas supprimer** (sinon erreur `CS5001`).

## Limites
Prototype front-end : données de démonstration, aucune API ni authentification réelle (voir `Services/DemoDataService.cs`).

## Dépannage
- `NU1301` / `NU1900` : vérifier l'accès à NuGet ou au cache local (`NU1900` n'est pas forcément bloquant).
- `MA002` : vérifier que `Microsoft.Maui.Controls` est bien restauré (cible Windows).
- Pas d'émulateur dans la liste : vérifier qu'il est créé et démarré dans Device Manager, ou activer le débogage USB sur le téléphone.
