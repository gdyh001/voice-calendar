"""
自然语言解析器：中文指令解析

从用户语音/文字输入中提取：
- 意图（添加/删除/查看）
- 日期（今天/明天/后天/周X/下周X/具体日期）
- 时间（X点/X点X分/上午/下午/晚上）
- 事件标题
"""

import re
from datetime import date, datetime, timedelta
from typing import Optional

# ---- 日期相关 ----

WEEKDAY_NAMES = {
    "周一": 0, "周二": 1, "周三": 2, "周四": 3, "周五": 4, "周六": 5, "周日": 6,
    "星期一": 0, "星期二": 1, "星期三": 2, "星期四": 3, "星期五": 4, "星期六": 5, "星期天": 6,
    "礼拜一": 0, "礼拜二": 1, "礼拜三": 2, "礼拜四": 3, "礼拜五": 4, "礼拜六": 5, "礼拜天": 6,
}

# 日期关键词 -> 偏移天数
DATE_KEYWORDS = {
    "今天": 0, "今日": 0,
    "明天": 1, "明日": 1,
    "后天": 2, "后日": 2,
    "大后天": 3,
}

# 时间数字映射
CN_NUM = {
    "一": 1, "二": 2, "三": 3, "四": 4, "五": 5,
    "六": 6, "七": 7, "八": 8, "九": 9, "十": 10,
    "十一": 11, "十二": 12, "两": 2, "零": 0,
}

# 意图关键词
ADD_KEYWORDS = ["添加", "新增", "增加", "加入", "加一个", "记一个", "记录", "提醒我", "提醒", "新建", "创建"]
DELETE_KEYWORDS = ["删除", "删掉", "去掉", "取消", "移除", "清除", "删", "去除"]
QUERY_KEYWORDS = ["查看", "查询", "看", "显示", "有什么", "有什么安排", "什么事", "有哪些", "列出"]


def parse_date(text: str, today: Optional[date] = None) -> Optional[date]:
    """
    从文本中提取日期。
    支持：今天/明天/后天、周X、下周X、X月X日/X月X号、XX月XX日
    返回 date 对象或 None
    """
    if today is None:
        today = date.today()

    text_clean = text.strip()

    # 1. 今天/明天/后天/大后天
    for kw, offset in DATE_KEYWORDS.items():
        if kw in text_clean:
            return today + timedelta(days=offset)

    # 2. 下周X
    next_week_match = re.search(r"下周([一二三四五六日天])", text_clean)
    if next_week_match:
        day_char = "周" + next_week_match.group(1)
        target_weekday = WEEKDAY_NAMES.get(day_char)
        if target_weekday is not None:
            days_ahead = target_weekday - today.weekday()
            if days_ahead <= 0:
                days_ahead += 7
            # 下周 = 再加 7 天
            return today + timedelta(days=days_ahead + 7)

    # 3. 周X / 星期X / 礼拜X
    for name, wd in WEEKDAY_NAMES.items():
        if name in text_clean:
            days_ahead = wd - today.weekday()
            if days_ahead < 0:
                days_ahead += 7
            elif days_ahead == 0:
                # 如果就是今天，可以用但更合理是本周的同一天=今天
                pass
            return today + timedelta(days=days_ahead)

    # 4. X月X日 / X月X号 / X月XX日
    month_day = re.search(r"(\d{1,2})\s*月\s*(\d{1,2})\s*[日号]", text_clean)
    if month_day:
        m = int(month_day.group(1))
        d = int(month_day.group(2))
        try:
            parsed = date(today.year, m, d)
            # 如果已过，推到明年
            if parsed < today:
                parsed = date(today.year + 1, m, d)
            return parsed
        except ValueError:
            return None

    return None


def parse_time(text: str) -> Optional[str]:
    """
    从文本中提取时间，返回 "HH:MM" 格式字符串。
    支持：
    - 上午/下午/晚上 + X点/X点X分/半点/一刻
    - 纯数字 X点 / XX:XX
    """
    hour = None
    minute = 0

    # 判断时段：上午、下午、晚上
    period = None
    if "上午" in text:
        period = "am"
    elif "下午" in text:
        period = "pm"
    elif "晚上" in text:
        period = "pm"
    elif "中午" in text:
        period = "pm"

    # 24小时制时间 HH:MM 或 HH：MM
    hhmm = re.search(r"(\d{1,2})\s*[:：]\s*(\d{2})", text)
    if hhmm:
        hour = int(hhmm.group(1))
        minute = int(hhmm.group(2))
        if 0 <= hour <= 23 and 0 <= minute <= 59:
            return f"{hour:02d}:{minute:02d}"
        return None

    # X点 / X点半 / X点X分 / X点一刻
    time_match = re.search(
        r"(\d{1,2}|[一二三四五六七八九十]{1,3})\s*点"
        r"(?:半|(?:(\d{1,2}|[一二三四五六七八九十]{1,3})\s*分)?|一刻)?",
        text
    )
    if time_match:
        # 解析小时
        hour_str = time_match.group(1)
        if hour_str.isdigit():
            hour = int(hour_str)
        else:
            hour = CN_NUM.get(hour_str)

        # 解析分钟
        match_full = time_match.group(0)
        if "半" in match_full:
            minute = 30
        elif "一刻" in match_full:
            minute = 15
        elif time_match.group(2):
            min_str = time_match.group(2)
            if min_str.isdigit():
                minute = int(min_str)
            else:
                minute = CN_NUM.get(min_str, 0)

    if hour is None:
        # 没有明确时间点但有"上午/下午/晚上"关键词
        if period == "am":
            return "09:00"
        elif period == "pm":
            return "15:00"
        return None

    # 根据时段调整小时
    if period == "pm" and hour < 12:
        hour += 12
    elif hour == 12 and period == "am":
        hour = 0

    if 0 <= hour <= 23 and 0 <= minute <= 59:
        return f"{hour:02d}:{minute:02d}"

    return None


def parse_intent(text: str) -> str:
    """
    识别意图：add / delete / query
    返回意图字符串，默认返回 "query"
    """
    for kw in ADD_KEYWORDS:
        if kw in text:
            return "add"
    for kw in DELETE_KEYWORDS:
        if kw in text:
            return "delete"
    for kw in QUERY_KEYWORDS:
        if kw in text:
            return "query"
    # 默认当作添加
    return "add"


def extract_title(text: str, parsed_date: Optional[date] = None,
                  parsed_time: Optional[str] = None) -> str:
    """
    从文本中提取事件标题（去掉日期时间关键词后的剩余内容）。
    """
    title = text

    # 去掉意图词
    for kw_list in [ADD_KEYWORDS, DELETE_KEYWORDS, QUERY_KEYWORDS]:
        for kw in sorted(kw_list, key=len, reverse=True):
            title = title.replace(kw, "")

    # 去掉日期词
    for kw in DATE_KEYWORDS:
        title = title.replace(kw, "")

    # 去掉周X
    for name in WEEKDAY_NAMES:
        title = title.replace(name, "")

    # 去掉 X月X日/X号
    title = re.sub(r"\d{1,2}\s*月\s*\d{1,2}\s*[日号]", "", title)
    # 去掉 HH:MM
    title = re.sub(r"\d{1,2}\s*[:：]\s*\d{2}", "", title)
    # 去掉 X点X分/半点/一刻
    title = re.sub(r"\d{1,2}\s*点\s*(半|一刻|\d{1,2}\s*分)?", "", title)
    # 去掉上午/下午/晚上/中午
    title = re.sub(r"(上午|下午|晚上|中午)", "", title)
    # 去掉下周
    title = title.replace("下周", "")
    # 去掉多余的标点和空格
    title = re.sub(r"[，,。.!！?？;；、]", "", title)
    title = title.strip()

    if not title:
        title = "未命名事件"

    return title


def parse_command(text: str, today: Optional[date] = None) -> dict:
    """
    一站式解析：将中文指令转为结构化数据。

    返回:
    {
        "action": "add" | "delete" | "query",
        "date": date对象或None,
        "time": "HH:MM"字符串或None,
        "title": 事件标题,
        "raw": 原始文本,
    }
    """
    if today is None:
        today = date.today()

    action = parse_intent(text)
    parsed_date = parse_date(text, today)
    parsed_time = parse_time(text)
    title = extract_title(text, parsed_date, parsed_time)

    return {
        "action": action,
        "date": parsed_date,
        "time": parsed_time,
        "title": title,
        "raw": text,
    }