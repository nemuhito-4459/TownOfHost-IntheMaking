# wiki フォルダについて

GitHub Wiki に貼り付けるためのページです。ファイル名がそのままWikiのページ名になります。

- `Home.md` … トップページ
- `_Sidebar.md` … 横のメニュー
- 陣営のページ（役職の一覧）：`Impostor.md` `Madmate.md` `Crewmate.md` `Jackal.md` `Neutral.md` `AssassinAndMerlin.md`
- 役職のページ：1役職1ページ（ファイル名は役職名。例：`アンチレポーター.md`）

役職の説明と設定の名前は `Resources/string.csv` の翻訳から作っています。

## 自動でWikiに反映
`main` に push して `wiki/` の中が変わると、GitHub Actions（`.github/workflows/SyncWiki.yml`）が GitHub Wiki にコピーします。
README.md はコピーしません。Actions の画面から手動で実行することもできます。
