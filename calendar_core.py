"""
日历核心模块：事件模型与 JSON 存储

提供事件的增删改查功能，使用 JSON 文件持久化存储。
"""

import json
import os
import uuid
from datetime import date, datetime
from typing import Optional

# 数据文件路径（与脚本同目录）
DATA_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "events.json")


class Event:
    """日历事件"""

    def __init__(
        self,
        title: str,
        event_date: date,
        event_time: Optional[str] = None,
        note: str = "",
        event_id: Optional[str] = None,
        created_at: Optional[str] = None,
    ):
        self.event_id = event_id or str(uuid.uuid4())[:8]
        self.title = title
        self.event_date = event_date  # datetime.date 对象
        self.event_time = event_time  # "HH:MM" 格式字符串，None 表示全天事件
        self.note = note
        self.created_at = created_at or datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    def to_dict(self) -> dict:
        """转为可 JSON 序列化的字典"""
        return {
            "event_id": self.event_id,
            "title": self.title,
            "event_date": self.event_date.isoformat(),
            "event_time": self.event_time,
            "note": self.note,
            "created_at": self.created_at,
        }

    @staticmethod
    def from_dict(data: dict) -> "Event":
        """从字典还原 Event 对象"""
        return Event(
            event_id=data.get("event_id"),
            title=data["title"],
            event_date=date.fromisoformat(data["event_date"]),
            event_time=data.get("event_time"),
            note=data.get("note", ""),
            created_at=data.get("created_at"),
        )

    def __repr__(self):
        time_str = self.event_time or "全天"
        return f"<Event {self.event_id}: [{self.event_date} {time_str}] {self.title}>"


def _load_events() -> list[Event]:
    """从 JSON 文件加载所有事件"""
    if not os.path.exists(DATA_FILE):
        return []
    try:
        with open(DATA_FILE, "r", encoding="utf-8") as f:
            data = json.load(f)
        return [Event.from_dict(item) for item in data]
    except (json.JSONDecodeError, KeyError):
        return []


def _save_events(events: list[Event]) -> None:
    """保存所有事件到 JSON 文件"""
    data = [event.to_dict() for event in events]
    with open(DATA_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)


def add_event(title: str, event_date: date, event_time: Optional[str] = None, note: str = "") -> Event:
    """添加新事件，返回创建的 Event 对象"""
    event = Event(title=title, event_date=event_date, event_time=event_time, note=note)
    events = _load_events()
    events.append(event)
    _save_events(events)
    return event


def delete_event(event_id: str) -> bool:
    """按 ID 删除事件，返回是否成功"""
    events = _load_events()
    new_events = [e for e in events if e.event_id != event_id]
    if len(new_events) == len(events):
        return False
    _save_events(new_events)
    return True


def delete_events_by_keyword(keyword: str, target_date: Optional[date] = None) -> int:
    """
    按关键词删除事件。
    如果指定 target_date，只在该日期内搜索；否则搜索所有事件。
    返回删除的事件数量。
    """
    events = _load_events()
    remaining = []
    deleted_count = 0
    for e in events:
        if keyword in e.title:
            if target_date is None or e.event_date == target_date:
                deleted_count += 1
                continue
        remaining.append(e)
    if deleted_count > 0:
        _save_events(remaining)
    return deleted_count


def get_events_by_date(target_date: date) -> list[Event]:
    """按日期查询事件，按时间排序"""
    events = _load_events()
    result = [e for e in events if e.event_date == target_date]
    result.sort(key=lambda e: e.event_time or "00:00")
    return result


def get_all_events() -> list[Event]:
    """获取所有事件，按日期和时间排序"""
    events = _load_events()
    events.sort(key=lambda e: (e.event_date, e.event_time or "00:00"))
    return events


def get_upcoming_events(limit: int = 10) -> list[Event]:
    """获取即将到来的事件（今天及以后）"""
    events = _load_events()
    today = date.today()
    upcoming = [e for e in events if e.event_date >= today]
    upcoming.sort(key=lambda e: (e.event_date, e.event_time or "00:00"))
    return upcoming[:limit]


def update_event(event_id: str, title: str = None, event_date: date = None,
                 event_time: str = None, note: str = None) -> Optional[Event]:
    """更新事件，返回更新后的 Event 或 None"""
    events = _load_events()
    for e in events:
        if e.event_id == event_id:
            if title is not None:
                e.title = title
            if event_date is not None:
                e.event_date = event_date
            if event_time is not None:
                e.event_time = event_time
            if note is not None:
                e.note = note
            _save_events(events)
            return e
    return None