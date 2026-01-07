# EasyUpdate

一个轻量级的 Windows 通用更新器，适用于任何需要自动更新功能的桌面应用程序。

## 特性

- **轻量级** - 单文件 EXE，体积 < 1MB，无需额外依赖
- **兼容性强** - 基于 .NET Framework 4.0，支持 Windows 7+
- **全自动** - 启动后自动下载、校验、替换，无需用户操作
- **支持批量更新** - 可同时更新多个文件
- **ZIP 支持** - 支持下载压缩包并自动解压
- **MD5 校验** - 可选的文件完整性校验
- **中文友好** - 正确处理中文文件名

## 使用方法

### 1. 准备配置文件

创建 JSON 配置：

```json
{
  "update_content": "1.修复网络异常bug\n2.优化启动速度",
  "main_process": "MyApp.exe",
  "main_exe": "MyApp.exe",
  "list": [
    {
      "name": "主程序",
      "url": "https://example.com/MyApp.zip",
      "md5": "d41d8cd98f00b204e9800998ecf8427e",
      "is_zip": true,
      "extract_name": "MyApp.exe",
      "save_path": ""
    },
    {
      "name": "配置文件",
      "url": "https://example.com/config.json",
      "is_zip": false,
      "save_path": "config"
    }
  ]
}
```

### 2. 生成 update 文件

将 JSON 进行 Base64 编码，保存为 `update` 文件（无扩展名）：

```csharp
string json = File.ReadAllText("config.json");
string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
File.WriteAllText("update", base64);
```

### 3. 启动更新器

将 `update.exe` 和 `update` 文件放在同一目录，运行 `update.exe` 即可。

## 配置说明

### UpdatePayload

| 字段             | 类型   | 必填 | 说明                       |
| ---------------- | ------ | ---- | -------------------------- |
| `update_content` | string | 否   | 更新说明，支持 `\n` 换行   |
| `list`           | array  | 是   | 更新文件列表               |
| `main_process`   | string | 否   | 主程序进程名，用于等待退出 |
| `main_exe`       | string | 否   | 更新完成后启动的程序       |

### UpdateListItem

| 字段           | 类型   | 必填 | 说明                        |
| -------------- | ------ | ---- | --------------------------- |
| `name`         | string | 否   | 显示名称，为空则从 URL 提取 |
| `url`          | string | 是   | 下载地址                    |
| `md5`          | string | 否   | MD5 校验值，为空则跳过校验  |
| `is_zip`       | bool   | 是   | 是否为压缩包                |
| `extract_name` | string | 否   | 解压后的目标文件名（重命名）|
| `save_path`    | string | 否   | 保存子目录，相对于程序目录  |

## 更新流程

```
启动 → 读取 update 文件 → 解析配置 → 删除 update 文件
                              ↓
              逐个下载文件 → MD5 校验 → 解压/复制
                              ↓
              等待主程序退出 → 替换文件 → 启动主程序
```

## 错误处理

| 场景         | 处理方式            |
| ------------ | ------------------- |
| 下载失败     | 重试 3 次，间隔递增 |
| MD5 校验失败 | 跳过该文件          |
| 文件替换失败 | 重试 10 次          |
| 进程等待超时 | 60 秒后强制继续     |

## 构建

```bash
# 使用 MSBuild
msbuild EasyUpdate.csproj /p:Configuration=Release

# 或使用 Visual Studio 打开 EasyUpdate.sln
```

## 系统要求

- Windows 7 / 8 / 10 / 11
- .NET Framework 4.0+

## License

MIT
