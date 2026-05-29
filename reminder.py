"""
提醒通知模块：后台定时检查 + Windows 桌面弹窗

每分钟检查一次即将到期的事件，弹出 Windows 原生通知。
"""

import threading
import time
from datetime import date, datetime
from typing import Callable, Optional


class ReminderService:
    """后台提醒服务"""

    def __init__(self, check_interval: int = 60):
        """
        check_interval: 检查间隔（秒），默认 60 秒
        """
        self.check_interval = check_interval
        self._running = False
        self._thread = None
        self._last_notified = set()  # 已通知的事件 ID（避免重复通知）
        self._on_remind: Optional[Callable] = None  # 回调函数

    def set_callback(self, callback: Callable):
        """设置提醒触发时的回调函数"""
        self._on_remind = callback

    def start(self):
        """启动后台线程"""
        if self._running:
            return
        self._running = True
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()

    def stop(self):
        """停止后台线程"""
        self._running = False
        if self._thread:
            self._thread.join(timeout=2)
            self._thread = None

    def _run(self):
        """后台循环检查"""
        while self._running:
            try:
                self._check_reminders()
            except Exception:
                pass
            time.sleep(self.check_interval)

    def _check_reminders(self):
        """检查是否有即将到期的事件"""
        from calendar_core import get_all_events

        now = datetime.now()
        today = now.date()
        current_time = now.strftime("%H:%M")

        events = get_all_events()
        for event in events:
            if event.event_time is None:
                continue  # 跳过全天事件

            # 检查是否应该提醒
            # 提醒条件：今天的事件 且 时间已到 且 未通知过
            if event.event_date == today and event.event_time == current_time:
                if event.event_id not in self._last_notified:
                    self._last_notified.add(event.event_id)
                    self._notify(event)

        # 清理旧的通知记录（每天重置）
        if len(self._last_notified) > 100:
            self._last_notified.clear()

    def _notify(self, event):
        """发送桌面通知"""
        from calendar_core import Event

        title = "日历提醒"
        message = event.title
        if event.event_time:
            message = event.event_time + " - " + event.title

        try:
            _show_notification(title, message)
        except Exception:
            # 降级：打印到控制台
            print(f"[提醒] {title}: {message}")

        # 调用外部回调
        if self._on_remind:
            try:
                self._on_remind(event)
            except Exception:
                pass


def _show_notification(title: str, message: str):
    """使用 plyer 发送 Windows 原生通知"""
    from plyer import notification
    notification.notify(
        title=title,
        message=message,
        app_name="语音日历工具",
        timeout=10,
    )


# 全局单例
_reminder_service = None


def get_reminder_service() -> ReminderService:
    """获取全局提醒服务实例"""
    global _reminder_service
    if _reminder_service is None:
        _reminder_service = ReminderService()
    return _reminder_service