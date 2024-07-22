if ($PSVersionTable.PSVersion -lt 7.2) { exit 1 }
$fileName = $args -join " "
$content = (Get-Content -Raw $fileName).ReplaceLineEndings("`n").TrimEnd("`n") + "`n"
$content.Replace("  ", "`t") | Set-Content -NoNewline $fileName
