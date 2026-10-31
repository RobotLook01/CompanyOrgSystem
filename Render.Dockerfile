        # ドロップダウン切り替え時に作動する大動脈関数（スマホ描画競合・完全解決版）
        function loadVersionData() {
            const selectedVersion = document.getElementById("versionDropdown").value;
            if(selectedVersion === "0") return;

            const imgTag = document.getElementById("productImage");
            const imageArea = document.getElementById("imageArea");
            const detailArea = document.getElementById("detailArea");

            # 💡【最重要】画像エリアをはじめから「表示状態」として固定します。
            # これにより、スマホのブラウザが先走ってエリアを消滅させるバグを根絶します。
            imageArea.style.display = "block";
            imageArea.className = "col-12 col-md-5 text-center";
            detailArea.className = "col-12 col-md-7";
            imgTag.style.opacity = "0"; # 読み込み中は一度透明にする（チラつき防止）

            # 画像が届いたらふわっとフェードインさせる
            imgTag.onload = function() {
                imgTag.style.opacity = "1";
            };

            # 画像が本当に無い（404等）の場合だけ、プレースホルダー等に切り替えるかそのままにする
            imgTag.onerror = function() {
                imgTag.style.opacity = "1";
                console.warn("画像が登録されていないか、読み込みに失敗しました。");
            };

            # キャッシュ破壊用のタイムスタンプを付与して画像をリクエスト
            imgTag.src = `/Product/GetImage?id=${productId}&version=${selectedVersion}&_=` + new Date().getTime();

            # --- 明細テーブルのFetch処理（ここは変更なし） ---
            const tbody = document.getElementById("detailTableBody");
            fetch(`/Product/GetDetails?id=${productId}&version=${selectedVersion}`)
                .then(response => {
                    if (!response.ok) throw new Error('通信エラー');
                    return response.json();
                })
                .then(data => {
                    tbody.innerHTML = "";
                    if (data && data.length > 0) {
                        data.forEach(row => {
                            const key = row.detail_key;
                            const val = row.detail_value;
                            const tr = document.createElement("tr");
                            tr.innerHTML = `<td class="ps-3"><strong>${escapeHtml(key)}</strong></td><td>${escapeHtml(val)}</td>`;
                            tbody.appendChild(tr);
                        });
                    } else {
                        tbody.innerHTML = `<tr><td colspan="2" class="text-center text-muted p-3">このバージョンの詳細・更新履歴データはありません。</td></tr>`;
                    }
                })
                .catch(error => {
                    console.error("明細の取得失敗:", error);
                    tbody.innerHTML = `<tr><td colspan="2" class="text-danger text-center p-3">データの読み込みに失敗しました。</td></tr>`;
                });
        }

        # 💡 古いイベント用の関数は空っぽにして、誤作動を完全に眠らせます
        function toggleImageArea(hasImage) {
            # 何もしない（上の固定配置にすべて委ねます）
        }
