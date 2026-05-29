"""
语音日历工具 - 主程序

tkinter GUI 入口，集成语音交互、日历视图、事件管理和提醒功能。
"""

import tkinter as tk
from tkinter import ttk, messagebox, simpledialog
from datetime import date, datetime, timedelta
import calendar
import threading

from calendar_core import (
    add_event, delete_event, delete_events_by_keyword,
    get_events_by_date, get_all_events, update_event, Event
)
from nlp_parser import parse_command
from reminder import get_reminder_service


class VoiceCalendarApp:
    """语音日历主应用"""

    def __init__(self):
        self.root = tk.Tk()
        self.root.title("语音日历工具")
        self.root.geometry("800x700")
        self.root.minsize(700, 600)

        # 当前选中的日期
        self.current_date = date.today()
        self.selected_date = date.today()

        # 日历网格中的 Label 组件缓存
        self._day_labels = []
        self._event_dots = {}  # date -> canvas oval id

        # 构建界面
        self._build_ui()
        self._refresh_all()

        # 启动后台提醒服务
        self._reminder = get_reminder_service()
        self._reminder.start()

        # 窗口关闭时停止提醒
        self.root.protocol("WM_DELETE_WINDOW", self._on_close)

    # ==================== UI 构建 ====================

    def _build_ui(self):
        """构建完整界面"""
        # 顶部标题栏
        self._build_header()

        # 中部主区域（左侧日历 + 右侧事件列表）
        main_pane = ttk.PanedWindow(self.root, orient=tk.HORIZONTAL)
        main_pane.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)

        # 左侧：日历
        left_frame = ttk.Frame(main_pane, width=420)
        main_pane.add(left_frame, weight=1)
        self._build_calendar(left_frame)

        # 右侧：事件列表 + 操作按钮
        right_frame = ttk.Frame(main_pane, width=340)
        main_pane.add(right_frame, weight=1)
        self._build_event_panel(right_frame)

        # 底部状态栏
        self._build_statusbar()

    def _build_header(self):
        """顶部标题栏"""
        header = ttk.Frame(self.root)
        header.pack(fill=tk.X, padx=10, pady=(10, 0))

        ttk.Label(
            header, text="语音日历工具",
            font=("Microsoft YaHei", 18, "bold")
        ).pack(side=tk.LEFT)

        # 语音输入按钮
        self.btn_voice = ttk.Button(
            header, text="🎤 语音输入",
            command=self._on_voice_input
        )
        self.btn_voice.pack(side=tk.RIGHT, padx=5)

        # 添加事件按钮
        self.btn_add = ttk.Button(
            header, text="+ 添加事件",
            command=self._on_add_event_form
        )
        self.btn_add.pack(side=tk.RIGHT, padx=5)

    def _build_calendar(self, parent):
        """构建月历视图"""
        # 月份导航
        nav_frame = ttk.Frame(parent)
        nav_frame.pack(fill=tk.X, pady=(0, 5))

        self.btn_prev = ttk.Button(nav_frame, text="◀", width=3, command=self._prev_month)
        self.btn_prev.pack(side=tk.LEFT)

        self.lbl_month = ttk.Label(
            nav_frame,
            text="",
            font=("Microsoft YaHei", 14, "bold"),
            anchor=tk.CENTER
        )
        self.lbl_month.pack(side=tk.LEFT, expand=True)

        self.btn_next = ttk.Button(nav_frame, text="▶", width=3, command=self._next_month)
        self.btn_next.pack(side=tk.RIGHT)

        # 今天按钮
        ttk.Button(nav_frame, text="今天", command=self._go_today).pack(side=tk.RIGHT, padx=5)

        # 星期标题
        week_header = ttk.Frame(parent)
        week_header.pack(fill=tk.X)
        days = ["一", "二", "三", "四", "五", "六", "日"]
        for d in days:
            lbl = ttk.Label(week_header, text=d, anchor=tk.CENTER, width=5)
            lbl.pack(side=tk.LEFT, expand=True, fill=tk.X)
            if d in ("六", "日"):
                lbl.configure(foreground="gray")

        # 日期网格（6行 x 7列）
        self.cal_grid = ttk.Frame(parent)
        self.cal_grid.pack(fill=tk.BOTH, expand=True)

        self._day_labels = []
        for row in range(6):
            row_frame = ttk.Frame(self.cal_grid)
            row_frame.pack(fill=tk.BOTH, expand=True)
            for col in range(7):
                cell_frame = ttk.Frame(row_frame, relief=tk.SOLID, borderwidth=1)
                cell_frame.pack(side=tk.LEFT, expand=True, fill=tk.BOTH)

                lbl = ttk.Label(
                    cell_frame, text="",
                    anchor=tk.N, justify=tk.CENTER,
                    font=("Microsoft YaHei", 10)
                )
                lbl.pack(expand=True, fill=tk.BOTH, padx=2, pady=2)
                lbl.bind("<Button-1>", lambda e, r=row, c=col: self._on_day_click(r, c))

                # 事件标记点（Canvas 画小圆点）
                canvas = tk.Canvas(cell_frame, width=40, height=6, highlightthickness=0)
                canvas.pack(side=tk.BOTTOM)

                self._day_labels.append((lbl, canvas, row, col))

    def _build_event_panel(self, parent):
        """构建右侧事件面板"""
        # 选中日期显示
        self.lbl_selected_date = ttk.Label(
            parent,
            text="",
            font=("Microsoft YaHei", 12, "bold")
        )
        self.lbl_selected_date.pack(fill=tk.X, pady=(0, 5))

        # 事件列表（Treeview）
        columns = ("time", "title")
        self.event_tree = ttk.Treeview(
            parent, columns=columns,
            show="headings", height=15
        )
        self.event_tree.heading("time", text="时间")
        self.event_tree.heading("title", text="事件")
        self.event_tree.column("time", width=60, anchor=tk.CENTER)
        self.event_tree.column("title", width=250)

        # 滚动条
        scrollbar = ttk.Scrollbar(parent, orient=tk.VERTICAL, command=self.event_tree.yview)
        self.event_tree.configure(yscrollcommand=scrollbar.set)

        self.event_tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        # 双击编辑事件
        self.event_tree.bind("<Double-1>", self._on_event_double_click)

        # 操作按钮
        btn_frame = ttk.Frame(parent)
        btn_frame.pack(fill=tk.X, pady=5)

        ttk.Button(
            btn_frame, text="编辑选中",
            command=self._on_edit_selected
        ).pack(side=tk.LEFT, padx=2)

        ttk.Button(
            btn_frame, text="删除选中",
            command=self._on_delete_selected
        ).pack(side=tk.LEFT, padx=2)

        ttk.Button(
            btn_frame, text="语音查看",
            command=self._on_voice_query
        ).pack(side=tk.RIGHT, padx=2)

    def _build_statusbar(self):
        """底部状态栏"""
        self.status_var = tk.StringVar(value="就绪")
        statusbar = ttk.Label(
            self.root, textvariable=self.status_var,
            relief=tk.SUNKEN, anchor=tk.W
        )
        statusbar.pack(side=tk.BOTTOM, fill=tk.X)

    # ==================== 日历渲染 ====================

    def _refresh_calendar(self):
        """刷新日历网格"""
        year = self.current_date.year
        month = self.current_date.month
        today = date.today()

        # 更新月份标题
        self.lbl_month.configure(text=f"{year}年 {month}月")

        # 计算该月第一天是周几、共几天
        first_day = date(year, month, 1)
        first_weekday = first_day.weekday()  # 0=周一
        days_in_month = calendar.monthrange(year, month)[1]

        # 加载本月所有事件用于标记
        events_this_month = get_all_events()
        event_dates = {}
        for e in events_this_month:
            if e.event_date.year == year and e.event_date.month == month:
                event_dates[e.event_date.day] = event_dates.get(e.event_date.day, 0) + 1

        day_num = 1
        for lbl, canvas, row, col in self._day_labels:
            if row == 0 and col < first_weekday:
                # 上月空白
                lbl.configure(text="", foreground="gray")
                canvas.delete("all")
                continue

            if day_num > days_in_month:
                # 下月空白
                lbl.configure(text="", foreground="gray")
                canvas.delete("all")
                continue

            # 设置日期数字
            lbl.configure(text=str(day_num))

            # 高亮今天
            cell_date = date(year, month, day_num)
            if cell_date == today:
                lbl.configure(foreground="white", background="#0078d4")
            elif cell_date == self.selected_date:
                lbl.configure(foreground="black", background="#cce5ff")
            else:
                lbl.configure(foreground="black", background="SystemButtonFace")

            # 周末灰色
            if col >= 5 and cell_date != self.selected_date and cell_date != today:
                lbl.configure(foreground="gray")

            # 事件标记点
            canvas.delete("all")
            if day_num in event_dates:
                count = event_dates[day_num]
                for i in range(min(count, 4)):
                    x = 5 + i * 8
                    canvas.create_oval(x, 1, x + 6, 7, fill="#0078d4", outline="")

            day_num += 1

    def _refresh_event_list(self):
        """刷新右侧事件列表"""
        for item in self.event_tree.get_children():
            self.event_tree.delete(item)

        self.lbl_selected_date.configure(
            text=f"{self.selected_date.strftime('%Y年%m月%d日')} 的事件"
        )

        events = get_events_by_date(self.selected_date)
        for e in events:
            time_str = e.event_time or "全天"
            self.event_tree.insert("", tk.END, values=(time_str, e.title), iid=e.event_id)

    def _refresh_all(self):
        """刷新整个界面"""
        self._refresh_calendar()
        self._refresh_event_list()

    # ==================== 事件操作 ====================

    def _on_day_click(self, row, col):
        """点击日历日期"""
        year = self.current_date.year
        month = self.current_date.month
        first_day = date(year, month, 1)
        first_weekday = first_day.weekday()
        days_in_month = calendar.monthrange(year, month)[1]

        day_num = row * 7 + col - first_weekday + 1
        if day_num < 1 or day_num > days_in_month:
            return

        self.selected_date = date(year, month, day_num)
        self._refresh_all()

    def _prev_month(self):
        """上个月"""
        if self.current_date.month == 1:
            self.current_date = date(self.current_date.year - 1, 12, 1)
        else:
            self.current_date = date(self.current_date.year, self.current_date.month - 1, 1)
        self._refresh_all()

    def _next_month(self):
        """下个月"""
        if self.current_date.month == 12:
            self.current_date = date(self.current_date.year + 1, 1, 1)
        else:
            self.current_date = date(self.current_date.year, self.current_date.month + 1, 1)
        self._refresh_all()

    def _go_today(self):
        """回到今天"""
        self.current_date = date.today()
        self.selected_date = date.today()
        self._refresh_all()

    def _on_delete_selected(self):
        """删除选中事件"""
        selection = self.event_tree.selection()
        if not selection:
            messagebox.showinfo("提示", "请先选中一个事件")
            return

        event_id = selection[0]
        values = self.event_tree.item(event_id, "values")
        title = values[1] if len(values) > 1 else ""

        if messagebox.askyesno("确认删除", f"确定要删除事件「{title}」吗？"):
            delete_event(event_id)
            self._refresh_all()
            self._set_status(f"已删除: {title}")

    def _on_edit_selected(self):
        """编辑选中事件"""
        selection = self.event_tree.selection()
        if not selection:
            messagebox.showinfo("提示", "请先选中一个事件")
            return
        event_id = selection[0]
        self._open_edit_dialog(event_id)

    def _on_event_double_click(self, event):
        """双击事件列表"""
        selection = self.event_tree.selection()
        if selection:
            self._open_edit_dialog(selection[0])

    def _open_edit_dialog(self, event_id):
        """打开事件编辑对话框"""
        events = get_events_by_date(self.selected_date)
        target = None
        for e in events:
            if e.event_id == event_id:
                target = e
                break

        if target is None:
            return

        dialog = tk.Toplevel(self.root)
        dialog.title("编辑事件")
        dialog.geometry("350x250")
        dialog.transient(self.root)
        dialog.grab_set()

        ttk.Label(dialog, text="事件名称:").pack(anchor=tk.W, padx=10, pady=(10, 0))
        title_var = tk.StringVar(value=target.title)
        ttk.Entry(dialog, textvariable=title_var, width=40).pack(fill=tk.X, padx=10)

        ttk.Label(dialog, text="日期 (YYYY-MM-DD):").pack(anchor=tk.W, padx=10, pady=(5, 0))
        date_var = tk.StringVar(value=target.event_date.isoformat())
        ttk.Entry(dialog, textvariable=date_var, width=40).pack(fill=tk.X, padx=10)

        ttk.Label(dialog, text="时间 (HH:MM，留空为全天):").pack(anchor=tk.W, padx=10, pady=(5, 0))
        time_var = tk.StringVar(value=target.event_time or "")
        ttk.Entry(dialog, textvariable=time_var, width=40).pack(fill=tk.X, padx=10)

        def save():
            title = title_var.get().strip()
            if not title:
                messagebox.showwarning("提示", "事件名称不能为空")
                return
            try:
                new_date = date.fromisoformat(date_var.get().strip())
            except ValueError:
                messagebox.showwarning("提示", "日期格式错误，请使用 YYYY-MM-DD")
                return
            new_time = time_var.get().strip() or None
            if new_time:
                import re
                if not re.match(r"^\d{2}:\d{2}$", new_time):
                    messagebox.showwarning("提示", "时间格式错误，请使用 HH:MM")
                    return

            update_event(event_id, title=title, event_date=new_date, event_time=new_time)
            dialog.destroy()
            self._refresh_all()
            self._set_status(f"已更新: {title}")

        btn_frame = ttk.Frame(dialog)
        btn_frame.pack(fill=tk.X, padx=10, pady=10)
        ttk.Button(btn_frame, text="保存", command=save).pack(side=tk.RIGHT, padx=5)
        ttk.Button(btn_frame, text="取消", command=dialog.destroy).pack(side=tk.RIGHT)

    def _on_add_event_form(self):
        """打开添加事件表单"""
        dialog = tk.Toplevel(self.root)
        dialog.title("添加事件")
        dialog.geometry("350x250")
        dialog.transient(self.root)
        dialog.grab_set()

        ttk.Label(dialog, text="事件名称:").pack(anchor=tk.W, padx=10, pady=(10, 0))
        title_var = tk.StringVar()
        ttk.Entry(dialog, textvariable=title_var, width=40).pack(fill=tk.X, padx=10)

        ttk.Label(dialog, text="日期 (YYYY-MM-DD):").pack(anchor=tk.W, padx=10, pady=(5, 0))
        date_var = tk.StringVar(value=self.selected_date.isoformat())
        ttk.Entry(dialog, textvariable=date_var, width=40).pack(fill=tk.X, padx=10)

        ttk.Label(dialog, text="时间 (HH:MM，留空为全天):").pack(anchor=tk.W, padx=10, pady=(5, 0))
        time_var = tk.StringVar()
        ttk.Entry(dialog, textvariable=time_var, width=40).pack(fill=tk.X, padx=10)

        def save():
            title = title_var.get().strip()
            if not title:
                messagebox.showwarning("提示", "事件名称不能为空")
                return
            try:
                ev_date = date.fromisoformat(date_var.get().strip())
            except ValueError:
                messagebox.showwarning("提示", "日期格式错误，请使用 YYYY-MM-DD")
                return
            ev_time = time_var.get().strip() or None
            if ev_time:
                import re
                if not re.match(r"^\d{2}:\d{2}$", ev_time):
                    messagebox.showwarning("提示", "时间格式错误，请使用 HH:MM")
                    return

            add_event(title=title, event_date=ev_date, event_time=ev_time)
            dialog.destroy()
            self._refresh_all()
            self._set_status(f"已添加: {title}")

        btn_frame = ttk.Frame(dialog)
        btn_frame.pack(fill=tk.X, padx=10, pady=10)
        ttk.Button(btn_frame, text="保存", command=save).pack(side=tk.RIGHT, padx=5)
        ttk.Button(btn_frame, text="取消", command=dialog.destroy).pack(side=tk.RIGHT)

    # ==================== 语音交互 ====================

    def _on_voice_input(self):
        """语音输入按钮回调"""
        self.btn_voice.configure(state=tk.DISABLED, text="正在录音...")
        self._set_status("正在录音，请说话（5秒）...")
        self.root.update()

        # 在后台线程执行语音识别（避免阻塞 GUI）
        def do_voice():
            try:
                from voice_engine import speech_to_text
                text = speech_to_text(duration=5.0)
            except ImportError as e:
                text = "[错误] 依赖未安装，请运行: pip install -r requirements.txt"
            except Exception as e:
                text = "[错误] " + str(e)

            # 回到主线程更新 UI
            self.root.after(0, lambda: self._on_voice_result(text))

        threading.Thread(target=do_voice, daemon=True).start()

    def _on_voice_result(self, text: str):
        """处理语音识别结果"""
        self.btn_voice.configure(state=tk.NORMAL, text="🎤 语音输入")

        if not text or text.startswith("["):
            self._set_status("语音识别失败: " + text)
            messagebox.showwarning("语音识别", f"识别失败:\n{text}")
            return

        self._set_status(f"识别结果: {text}")

        # 解析指令
        parsed = parse_command(text, date.today())

        # 根据意图执行
        if parsed["action"] == "add":
            if parsed["date"] is None:
                # 没识别出日期，用今天
                parsed["date"] = date.today()

            event = add_event(
                title=parsed["title"],
                event_date=parsed["date"],
                event_time=parsed["time"]
            )
            self.selected_date = parsed["date"]
            self.current_date = parsed["date"]
            self._refresh_all()
            self._set_status(f"已添加: {event.title} ({event.event_date} {event.event_time or '全天'})")

        elif parsed["action"] == "delete":
            target_date = parsed["date"]
            count = delete_events_by_keyword(parsed["title"], target_date)
            if count > 0:
                self._refresh_all()
                self._set_status(f"已删除 {count} 个事件")
            else:
                self._set_status("未找到匹配的事件")
                messagebox.showinfo("提示", f"未找到与「{parsed['title']}」匹配的事件")

        elif parsed["action"] == "query":
            if parsed["date"] is not None:
                self.selected_date = parsed["date"]
                self.current_date = parsed["date"]
            self._refresh_all()

            events = get_events_by_date(self.selected_date)
            if events:
                names = ", ".join(e.title for e in events)
                self._set_status(f"{self.selected_date}: {names}")
            else:
                self._set_status(f"{self.selected_date}: 暂无事件")

    def _on_voice_query(self):
        """语音查看按钮"""
        dialog = tk.Toplevel(self.root)
        dialog.title("语音查询")
        dialog.geometry("350x150")
        dialog.transient(self.root)
        dialog.grab_set()

        ttk.Label(dialog, text="请输入要查看的日期（日期/周X/今天等）:", font=("Microsoft YaHei", 10)).pack(pady=10)
        query_var = tk.StringVar()
        ttk.Entry(dialog, textvariable=query_var, width=40).pack(padx=10)

        def do_query():
            text = query_var.get().strip()
            if not text:
                return
            parsed = parse_command(text, date.today())
            if parsed["date"] is not None:
                self.selected_date = parsed["date"]
                self.current_date = parsed["date"]
                self._refresh_all()
            dialog.destroy()

        ttk.Button(dialog, text="查询", command=do_query).pack(pady=10)

    # ==================== 工具方法 ====================

    def _set_status(self, msg: str):
        """更新状态栏"""
        self.status_var.set(msg)

    def _on_close(self):
        """关闭窗口"""
        self._reminder.stop()
        # 清理语音模型
        try:
            from voice_engine import cleanup_model
            cleanup_model()
        except Exception:
            pass
        self.root.destroy()

    def run(self):
        """启动应用"""
        self.root.mainloop()


def main():
    """程序入口"""
    app = VoiceCalendarApp()
    app.run()


if __name__ == "__main__":
    main()