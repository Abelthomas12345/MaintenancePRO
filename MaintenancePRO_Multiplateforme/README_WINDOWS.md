# MaintenancePRO — version Windows (.NET MAUI 10)

Cette variante cible uniquement Windows et reprend les interfaces XAML du projet MaintenancePRO retravaillé.

## Ouvrir
1. Décompresser l’archive.
2. Ouvrir `MaintenancePRO.csproj` dans Visual Studio Community 2022 avec le workload **Développement d’interface utilisateur d’applications .NET multiplateformes** et les outils Windows MAUI installés.
3. Sélectionner `Windows Machine`, configuration `Debug`, puis lancer avec F5.

## Dépendances
Le projet utilise `Microsoft.Maui.Controls` 10.0.20 et `Microsoft.Extensions.Logging.Debug` 10.0.0, alignés sur la variante Windows de référence. NuGet doit pouvoir restaurer ces packages, ou ils doivent déjà être présents dans le cache local. Aucun package tiers d’interface n’est ajouté.

## Limites
Les vues et interactions front-end utilisent des données de démonstration. Les actions qui indiquent une validation locale ou une future liaison backend ne transmettent pas encore de données à une API.

## Dépannage
- Si la restauration échoue avec `NU1301` ou `NU1900`, vérifier l’accès à NuGet ou le cache local. `NU1900` concerne la vérification des vulnérabilités et n’est pas nécessairement une erreur de compilation.
- Si `MA002` persiste, vérifier que la référence `Microsoft.Maui.Controls` est bien restaurée.
- `CS5001` (pas de méthode Main statique) : le dossier `Platforms/Windows` (point d'entrée WinUI : `App.xaml`, `App.xaml.cs`, `app.manifest`, `Package.appxmanifest`) était manquant. Il est maintenant inclus : ne pas le supprimer.
