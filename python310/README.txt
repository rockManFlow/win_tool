Python 3.10.0 随应用打包说明（用户无需单独安装 Python）
============================================================

程序会优先使用本目录下的：

  <程序目录>\tools\python310\python.exe

请在本机构建/发布前，将「嵌入式 Python 3.10.0 + pdf2docx==0.5.8」准备好并放入
tools\python310\（可与程序一并复制给用户）。

方式一：使用脚本自动准备（推荐，需联网一次）
------------------------------------------
在 tools\python310\ 目录下以管理员或普通用户打开 PowerShell，执行：

  powershell -ExecutionPolicy Bypass -File .\install-bundled.ps1

脚本会下载官方 Windows amd64 嵌入式包、配置 pip、并按 requirements.txt 安装依赖。

方式二：手动准备
----------------
1. 下载 Python 3.10.0 Windows embeddable package（amd64）：
   https://www.python.org/ftp/python/3.10.0/python-3.10.0-embed-amd64.zip

2. 解压全部文件到本目录 tools\python310\（与 python.exe 同级）。

3. 编辑 python310._pth：取消最后一行「import site」的注释，或新增一行 import site。

4. 下载 get-pip.py（https://bootstrap.pypa.io/get-pip.py）到本目录，执行：
     .\python.exe get-pip.py

5. 安装依赖：
     .\python.exe -m pip install -r requirements.txt

6. 验证：
     .\python.exe -c "import pdf2docx; print(pdf2docx.__version__)"

注意：不要将本目录完整提交到 Git（体积大）。仓库中仅保留 README、requirements、
install-bundled.ps1；发布时在构建机上生成 tools\python310 再打入安装包。
