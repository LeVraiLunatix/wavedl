# WaveDL

Application Windows 11 native (WinUI 3 / .NET 9) pour télécharger **légalement** les musiques
que vous avez le droit de récupérer, depuis YouTube et YouTube Music.

> WaveDL ne contourne **aucune** protection DRM. Les liens Spotify / Deezer / Apple Music
> servent uniquement à **identifier** un morceau (titre, artiste, album, pochette) afin de
> retrouver la version correspondante sur YouTube Music.

## Fonctionnalités

- Recherche intégrée façon Apple Music (pochette, titre, artiste, durée)
- Collage d'un lien YouTube / YouTube Music (morceau, playlist, album)
- Import d'un lien Spotify / Deezer / Apple Music → correspondances YouTube Music notées par score de confiance
- Téléchargements parallèles avec carte de progression (vitesse, temps restant, format)
- Formats : MP3 320 kbps, FLAC, WAV, AAC — meilleure source audio sélectionnée automatiquement
- Historique SQLite (pochette, date, qualité, emplacement, ouvrir le dossier, supprimer)
- Pause / reprise, reprise après interruption (`yt-dlp --continue`)
- Limitation de vitesse, dossier de destination configurable
- Détection du presse-papiers, glisser-déposer d'un lien
- Notifications Windows
- Vérification automatique des mises à jour (GitHub Releases)
- Fluent Design, fond Mica, mode sombre par défaut, accent bleu électrique

## Prérequis de build

- Windows 11 (ou Windows 10 19041+)
- Visual Studio 2022 17.11+ avec la charge de travail **« Développement d'applications de bureau .NET »**
  et le composant **Windows App SDK C# Templates**
- .NET 9 SDK

Ouvrir `WaveDL.sln`, sélectionner la plateforme (`x64` recommandé) puis **F5**.
Le premier lancement propose d'installer `yt-dlp` et `FFmpeg` automatiquement
(dans `%LOCALAPPDATA%\WaveDL\bin`). Vous pouvez aussi placer `yt-dlp.exe` / `ffmpeg.exe`
à côté de l'exécutable ou dans le `PATH`.

## Ligne de commande

```bash
dotnet build src/WaveDL/WaveDL.csproj -c Debug
```

Publication autonome (aucune dépendance runtime à installer) :

```bash
dotnet publish src/WaveDL/WaveDL.csproj -c Release -r win-x64 --self-contained -p:WindowsAppSDKSelfContained=true
```

## Architecture

```
src/WaveDL
├── Assets/         Icônes et ressources
├── Data/           EF Core (SQLite) : contexte + entité d'historique
├── Helpers/        Convertisseurs, similarité de texte, formatage, logger fichier
├── Models/         Types métier immuables (Track, DownloadRequest, ...)
├── Services/       Logique métier isolée de l'UI
│   ├── Abstractions/   Interfaces des services
│   ├── Providers/      Résolution des métadonnées Spotify / Deezer / Apple Music
│   └── YtDlp/          Pilotage du processus yt-dlp + parsing
├── ViewModels/     MVVM (CommunityToolkit.Mvvm)
└── Views/          Pages WinUI 3
```

Toute la logique métier vit dans `Services/` ; les `ViewModels/` orchestrent, les `Views/`
n'ont pas de logique. L'injection de dépendances est configurée dans `App.xaml.cs`.

## Publier une version (distribution publique)

Le workflow [`.github/workflows/release.yml`](.github/workflows/release.yml) se déclenche sur
un tag `v*` :

```bash
git tag v1.0.0
git push origin v1.0.0
```

Il produit, sur `windows-latest` :

- `WaveDL-Setup-1.0.0.exe` — installateur par-utilisateur (Inno Setup, sans admin,
  vers `%LOCALAPPDATA%\Programs\WaveDL`)
- `WaveDL-1.0.0-win-x64-portable.zip` et `…-win-arm64-portable.zip` — versions décompressables

puis crée une **Release GitHub en brouillon** avec ces trois fichiers. Il suffit de la publier.
Le bouton « Mettre à jour » de l'app et le site pointent sur `releases/latest`.

Script installateur : [`installer/WaveDL.iss`](installer/WaveDL.iss).

## Site de présentation

Dépôt séparé `wavedl-site` (une page statique) → à déployer sur Vercel (`wavedl.vercel.app`).
Les CTA de téléchargement pointent vers les Releases de ce dépôt.

## Légal

N'utilisez WaveDL que pour du contenu dont vous détenez les droits (vos propres œuvres,
domaine public, licences Creative Commons, ou autorisation explicite de l'ayant droit).
Le respect des conditions d'utilisation des plateformes tierces est de votre responsabilité.
