<?php
declare(strict_types=1);
?>
<!doctype html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">

    <title>UTM Monitor</title>

    <style>
        :root {
            --bg: #080b10;
            --bg2: #0d1118;
            --panel: #11161e;
            --panel2: #151b24;
            --panel3: #1a212c;
            --border: rgba(255,255,255,.08);
            --border2: rgba(255,255,255,.12);

            --text: #f1f5f9;
            --muted: #8d98a8;
            --muted2: #667181;

            --green: #35d48a;
            --green-bg: rgba(53,212,138,.10);

            --red: #ff5d6c;
            --red-bg: rgba(255,93,108,.10);

            --yellow: #f5c451;
            --yellow-bg: rgba(245,196,81,.10);

            --blue: #5b9cff;
            --blue-bg: rgba(91,156,255,.10);

            --purple: #a78bfa;
            --cyan: #45d7e8;

            --radius: 16px;
            --radius-sm: 11px;

            --sidebar: 240px;
        }

        * {
            box-sizing: border-box;
        }

        html {
            scroll-behavior: smooth;
        }

        body {
            margin: 0;
            background:
                radial-gradient(circle at 80% -10%, rgba(91,156,255,.10), transparent 35%),
                radial-gradient(circle at 10% 20%, rgba(53,212,138,.045), transparent 30%),
                var(--bg);
            color: var(--text);
            font-family:
                Inter,
                -apple-system,
                BlinkMacSystemFont,
                "Segoe UI",
                Arial,
                sans-serif;
            min-height: 100vh;
        }

        button,
        input,
        select {
            font: inherit;
        }

        button {
            cursor: pointer;
        }

        /* =========================
           LAYOUT
        ========================= */

        .app {
            min-height: 100vh;
        }

        .sidebar {
            position: fixed;
            inset: 0 auto 0 0;
            width: var(--sidebar);
            background: rgba(10,14,20,.94);
            border-right: 1px solid var(--border);
            padding: 22px 14px;
            z-index: 50;
            backdrop-filter: blur(18px);
        }

        .brand {
            display: flex;
            align-items: center;
            gap: 11px;
            padding: 5px 10px 24px;
        }

        .brand-logo {
            width: 38px;
            height: 38px;
            border-radius: 12px;
            display: grid;
            place-items: center;
            background: linear-gradient(135deg, #5b9cff, #35d48a);
            color: #fff;
            font-weight: 900;
            box-shadow: 0 8px 28px rgba(91,156,255,.22);
        }

        .brand-title {
            font-size: 16px;
            font-weight: 800;
            letter-spacing: .2px;
        }

        .brand-subtitle {
            font-size: 11px;
            color: var(--muted);
            margin-top: 2px;
        }

        .nav-title {
            padding: 8px 11px;
            color: var(--muted2);
            font-size: 10px;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: .12em;
        }

        .nav {
            display: flex;
            flex-direction: column;
            gap: 4px;
        }

        .nav button {
            width: 100%;
            border: 0;
            background: transparent;
            color: var(--muted);
            padding: 11px 12px;
            border-radius: 10px;
            display: flex;
            align-items: center;
            gap: 11px;
            text-align: left;
            transition: .18s ease;
        }

        .nav button:hover {
            color: var(--text);
            background: rgba(255,255,255,.045);
        }

        .nav button.active {
            color: #fff;
            background: rgba(91,156,255,.11);
        }

        .nav-icon {
            width: 20px;
            text-align: center;
            opacity: .85;
        }

        .main {
            margin-left: var(--sidebar);
            padding: 28px 32px 100px;
            max-width: 1700px;
        }

        /* =========================
           TOPBAR
        ========================= */

        .topbar {
            display: flex;
            justify-content: space-between;
            align-items: center;
            gap: 20px;
            margin-bottom: 28px;
        }

        .eyebrow {
            color: var(--blue);
            font-size: 11px;
            font-weight: 800;
            text-transform: uppercase;
            letter-spacing: .13em;
            margin-bottom: 7px;
        }

        h1 {
            margin: 0;
            font-size: 30px;
            line-height: 1.1;
            letter-spacing: -.6px;
        }

        .subtitle {
            color: var(--muted);
            margin-top: 8px;
            font-size: 14px;
        }

        .top-actions {
            display: flex;
            gap: 8px;
            align-items: center;
            flex-wrap: wrap;
        }

        .btn {
            border: 1px solid var(--border2);
            background: var(--panel);
            color: var(--text);
            border-radius: 10px;
            padding: 10px 14px;
            transition: .18s ease;
            white-space: nowrap;
        }

        .btn:hover {
            background: var(--panel3);
            border-color: rgba(255,255,255,.18);
        }

        .btn-primary {
            border-color: transparent;
            background: linear-gradient(135deg, #5b9cff, #397ee8);
            box-shadow: 0 8px 24px rgba(91,156,255,.16);
        }

        .btn-primary:hover {
            filter: brightness(1.08);
        }

        .btn-danger {
            color: #ff7c87;
            border-color: rgba(255,93,108,.18);
            background: rgba(255,93,108,.06);
        }

        .btn-small {
            padding: 7px 10px;
            font-size: 12px;
        }

        /* =========================
           HERO
        ========================= */

        .hero {
            position: relative;
            overflow: hidden;
            border: 1px solid var(--border);
            background:
                linear-gradient(135deg, rgba(91,156,255,.10), rgba(53,212,138,.045)),
                var(--panel);
            border-radius: 20px;
            padding: 25px;
            margin-bottom: 18px;
        }

        .hero:after {
            content: "";
            position: absolute;
            width: 240px;
            height: 240px;
            border-radius: 50%;
            right: -100px;
            top: -120px;
            background: rgba(91,156,255,.08);
            filter: blur(5px);
        }

        .hero h2 {
            margin: 0;
            font-size: 22px;
        }

        .hero p {
            color: var(--muted);
            margin: 8px 0 0;
        }

        /* =========================
           METRICS
        ========================= */

        .metrics {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 12px;
            margin-bottom: 28px;
        }

        .metric {
            border: 1px solid var(--border);
            background: var(--panel);
            border-radius: var(--radius);
            padding: 18px;
            min-height: 115px;
        }

        .metric-head {
            display: flex;
            justify-content: space-between;
            color: var(--muted);
            font-size: 12px;
        }

        .metric-icon {
            width: 30px;
            height: 30px;
            display: grid;
            place-items: center;
            border-radius: 9px;
            background: rgba(255,255,255,.045);
        }

        .metric-value {
            font-size: 29px;
            font-weight: 800;
            margin-top: 13px;
        }

        .metric-caption {
            color: var(--muted2);
            font-size: 11px;
            margin-top: 3px;
        }

        /* =========================
           SECTIONS
        ========================= */

        .section {
            margin-top: 30px;
            scroll-margin-top: 20px;
        }

        .section-head {
            display: flex;
            align-items: flex-end;
            justify-content: space-between;
            gap: 15px;
            margin-bottom: 13px;
        }

        .section-title {
            font-size: 18px;
            font-weight: 800;
        }

        .section-subtitle {
            color: var(--muted);
            font-size: 12px;
            margin-top: 4px;
        }

        .section-tools {
            display: flex;
            align-items: center;
            gap: 7px;
            flex-wrap: wrap;
        }

        /* =========================
           UTM CARDS
        ========================= */

        .utm-grid {
            display: grid;
            grid-template-columns: repeat(2, minmax(0,1fr));
            gap: 14px;
        }

        .utm-card {
            border: 1px solid var(--border);
            background: linear-gradient(180deg, var(--panel), rgba(17,22,30,.84));
            border-radius: 17px;
            overflow: hidden;
        }

        .utm-card.online {
            border-color: rgba(53,212,138,.14);
        }

        .utm-card.offline {
            border-color: rgba(255,93,108,.18);
        }

        .utm-card-top {
            padding: 18px;
            display: flex;
            justify-content: space-between;
            gap: 15px;
        }

        .utm-name {
            font-size: 17px;
            font-weight: 800;
        }

        .utm-address {
            color: var(--muted);
            font-size: 12px;
            margin-top: 5px;
            font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
        }

        .status-pill {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            border-radius: 999px;
            padding: 6px 9px;
            font-size: 10px;
            font-weight: 800;
            letter-spacing: .04em;
        }

        .status-pill.online {
            color: var(--green);
            background: var(--green-bg);
        }

        .status-pill.offline {
            color: var(--red);
            background: var(--red-bg);
        }

        .dot {
            width: 7px;
            height: 7px;
            border-radius: 50%;
            background: currentColor;
            box-shadow: 0 0 8px currentColor;
        }

        .utm-stats {
            display: grid;
            grid-template-columns: repeat(4,1fr);
            border-top: 1px solid var(--border);
            border-bottom: 1px solid var(--border);
        }

        .utm-stat {
            padding: 13px 14px;
            min-width: 0;
        }

        .utm-stat + .utm-stat {
            border-left: 1px solid var(--border);
        }

        .utm-stat-label {
            color: var(--muted2);
            font-size: 10px;
            margin-bottom: 5px;
        }

        .utm-stat-value {
            font-size: 13px;
            font-weight: 700;
        }

        .valid {
            color: var(--green);
        }

        .warning {
            color: var(--yellow);
        }

        .danger {
            color: var(--red);
        }

        .utm-card-actions {
            display: flex;
            gap: 7px;
            padding: 13px;
            flex-wrap: wrap;
        }

        /* =========================
           TTN
        ========================= */

        .filters {
            display: flex;
            align-items: center;
            gap: 7px;
            flex-wrap: wrap;
            margin-bottom: 12px;
        }

        .filter {
            border: 1px solid var(--border);
            background: var(--panel);
            color: var(--muted);
            border-radius: 999px;
            padding: 8px 12px;
            font-size: 11px;
        }

        .filter.active {
            color: #fff;
            border-color: rgba(91,156,255,.35);
            background: rgba(91,156,255,.11);
        }

        .search {
            flex: 1;
            min-width: 220px;
            border: 1px solid var(--border);
            background: var(--panel);
            color: var(--text);
            outline: none;
            border-radius: 10px;
            padding: 10px 12px;
        }

        .search:focus,
        select:focus,
        input:focus {
            border-color: rgba(91,156,255,.45);
            box-shadow: 0 0 0 3px rgba(91,156,255,.07);
        }

        select {
            border: 1px solid var(--border);
            background: var(--panel);
            color: var(--text);
            border-radius: 10px;
            padding: 9px 11px;
            outline: none;
        }

        .table-wrap {
            border: 1px solid var(--border);
            background: var(--panel);
            border-radius: 16px;
            overflow: auto;
        }

        table {
            width: 100%;
            border-collapse: collapse;
            min-width: 900px;
        }

        th {
            text-align: left;
            padding: 12px 14px;
            color: var(--muted2);
            font-size: 10px;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: .06em;
            background: rgba(255,255,255,.018);
            border-bottom: 1px solid var(--border);
        }

        td {
            padding: 13px 14px;
            border-bottom: 1px solid rgba(255,255,255,.045);
            font-size: 12px;
            vertical-align: middle;
        }

        tr:last-child td {
            border-bottom: 0;
        }

        tbody tr {
            transition: .14s ease;
        }

        tbody tr:hover {
            background: rgba(255,255,255,.025);
        }

        .ttn-number {
            color: #fff;
            font-weight: 800;
        }

        .small {
            font-size: 11px;
            color: var(--muted);
        }

        .mark-count {
            color: var(--green);
            font-weight: 800;
        }

        .direction {
            font-size: 10px;
            font-weight: 800;
            border-radius: 999px;
            padding: 5px 8px;
        }

        .direction.in {
            color: var(--blue);
            background: var(--blue-bg);
        }

        .direction.out {
            color: var(--purple);
            background: rgba(167,139,250,.10);
        }

        /* =========================
           PROBLEMS
        ========================= */

        .problem-box {
            border: 1px solid var(--border);
            background: var(--panel);
            border-radius: 16px;
            padding: 17px;
        }

        .problem-ok {
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .problem-ok-icon {
            width: 35px;
            height: 35px;
            border-radius: 10px;
            display: grid;
            place-items: center;
            color: var(--green);
            background: var(--green-bg);
            font-weight: 900;
        }

        /* =========================
           TECHNICAL
        ========================= */

        .technical {
            border: 1px solid var(--border);
            border-radius: 16px;
            background: var(--panel);
            overflow: hidden;
        }

        .technical summary {
            list-style: none;
            cursor: pointer;
            padding: 16px 18px;
            font-weight: 700;
        }

        .technical summary::-webkit-details-marker {
            display: none;
        }

        .technical-content {
            padding: 0 18px 18px;
            color: var(--muted);
            font-size: 12px;
            line-height: 1.65;
        }

        .tech-code {
            display: inline-block;
            padding: 3px 6px;
            border-radius: 5px;
            background: rgba(255,255,255,.05);
            color: #cbd5e1;
            font-family: ui-monospace, monospace;
            font-size: 11px;
        }

        /* =========================
           FOOTER
        ========================= */

        footer {
            margin-top: 45px;
            padding: 20px 0;
            color: var(--muted2);
            font-size: 11px;
            border-top: 1px solid var(--border);
        }

        /* =========================
           FLOATING UTM
        ========================= */

        .floating-utm {
            position: fixed;
            right: 20px;
            bottom: 20px;
            width: 245px;
            border: 1px solid var(--border2);
            background: rgba(13,17,24,.94);
            backdrop-filter: blur(20px);
            border-radius: 15px;
            box-shadow: 0 18px 55px rgba(0,0,0,.42);
            z-index: 100;
            overflow: hidden;
        }

        .floating-head {
            padding: 11px 13px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 1px solid var(--border);
        }

        .floating-title {
            font-size: 11px;
            font-weight: 800;
        }

        .floating-total {
            font-size: 10px;
            color: var(--muted);
        }

        .floating-item {
            padding: 10px 13px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 10px;
            cursor: pointer;
        }

        .floating-item:hover {
            background: rgba(255,255,255,.035);
        }

        .floating-left {
            min-width: 0;
        }

        .floating-name {
            font-size: 11px;
            font-weight: 700;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .floating-address {
            color: var(--muted2);
            font-size: 9px;
            margin-top: 2px;
        }

        .floating-status {
            display: flex;
            align-items: center;
            gap: 5px;
            font-size: 9px;
            font-weight: 800;
        }

        .floating-status.online {
            color: var(--green);
        }

        .floating-status.offline {
            color: var(--red);
        }

        .floating-add {
            margin: 7px 9px 9px;
            width: calc(100% - 18px);
            padding: 8px;
            font-size: 10px;
        }

        /* =========================
           TOAST
        ========================= */

        #toasts {
            position: fixed;
            top: 20px;
            right: 20px;
            z-index: 1000;
            display: flex;
            flex-direction: column;
            gap: 8px;
            width: 330px;
            pointer-events: none;
        }

        .toast {
            pointer-events: auto;
            border: 1px solid var(--border2);
            background: rgba(17,22,30,.96);
            backdrop-filter: blur(16px);
            border-radius: 12px;
            padding: 12px 14px;
            box-shadow: 0 14px 35px rgba(0,0,0,.35);
            animation: toastIn .2s ease;
        }

        .toast-title {
            font-size: 12px;
            font-weight: 800;
        }

        .toast-text {
            color: var(--muted);
            font-size: 11px;
            margin-top: 3px;
            line-height: 1.4;
        }

        .toast.success {
            border-color: rgba(53,212,138,.22);
        }

        .toast.error {
            border-color: rgba(255,93,108,.24);
        }

        .toast.warning {
            border-color: rgba(245,196,81,.24);
        }

        @keyframes toastIn {
            from {
                opacity: 0;
                transform: translateY(-8px);
            }
            to {
                opacity: 1;
                transform: translateY(0);
            }
        }

        /* =========================
           MODAL
        ========================= */

        .modal-backdrop {
            position: fixed;
            inset: 0;
            background: rgba(0,0,0,.66);
            backdrop-filter: blur(7px);
            z-index: 500;
            display: none;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }

        .modal-backdrop.open {
            display: flex;
        }

        .modal {
            width: min(760px,100%);
            max-height: 90vh;
            overflow: auto;
            background: #10151d;
            border: 1px solid var(--border2);
            border-radius: 18px;
            box-shadow: 0 30px 90px rgba(0,0,0,.55);
        }

        .modal.small-modal {
            width: min(440px,100%);
        }

        .modal-head {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 18px 20px;
            border-bottom: 1px solid var(--border);
        }

        .modal-title {
            font-size: 16px;
            font-weight: 800;
        }

        .modal-close {
            border: 0;
            background: transparent;
            color: var(--muted);
            font-size: 21px;
            line-height: 1;
        }

        .modal-body {
            padding: 20px;
        }

        .modal-actions {
            display: flex;
            justify-content: flex-end;
            gap: 8px;
            padding: 15px 20px;
            border-top: 1px solid var(--border);
        }

        .form-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 13px;
        }

        .field {
            display: flex;
            flex-direction: column;
            gap: 6px;
        }

        .field.full {
            grid-column: 1 / -1;
        }

        .field label {
            color: var(--muted);
            font-size: 11px;
        }

        .field input {
            border: 1px solid var(--border);
            background: var(--bg2);
            color: var(--text);
            border-radius: 10px;
            padding: 11px;
            outline: none;
        }

        .confirm-text {
            color: var(--muted);
            line-height: 1.55;
            font-size: 13px;
        }

        /* =========================
           DOCUMENT MODAL
        ========================= */

        .document-modal {
            width: min(1100px,100%);
        }

        .document-header {
            display: grid;
            grid-template-columns: 1fr auto;
            gap: 20px;
            padding: 20px;
            border-bottom: 1px solid var(--border);
        }

        .document-number {
            font-size: 24px;
            font-weight: 850;
        }

        .document-meta {
            color: var(--muted);
            font-size: 11px;
            margin-top: 5px;
        }

        .doc-status {
            align-self: start;
        }

        .doc-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 12px;
            padding: 18px 20px;
        }

        .doc-info-card {
            border: 1px solid var(--border);
            background: rgba(255,255,255,.018);
            border-radius: 12px;
            padding: 14px;
        }

        .doc-info-title {
            color: var(--muted2);
            text-transform: uppercase;
            letter-spacing: .06em;
            font-size: 9px;
            font-weight: 800;
            margin-bottom: 7px;
        }

        .doc-info-value {
            font-size: 12px;
            line-height: 1.45;
        }

        .doc-content {
            padding: 0 20px 20px;
        }

        .doc-section-title {
            font-size: 13px;
            font-weight: 800;
            margin: 18px 0 9px;
        }

        .marks {
            display: flex;
            flex-wrap: wrap;
            gap: 5px;
            max-height: 180px;
            overflow: auto;
        }

        .mark {
            font-family: ui-monospace, monospace;
            font-size: 9px;
            background: rgba(255,255,255,.045);
            color: #cbd5e1;
            border-radius: 5px;
            padding: 4px 6px;
        }

        .timeline {
            border-left: 1px solid var(--border2);
            margin-left: 7px;
            padding-left: 18px;
        }

        .timeline-item {
            position: relative;
            margin-bottom: 15px;
        }

        .timeline-item:before {
            content: "";
            position: absolute;
            width: 7px;
            height: 7px;
            border-radius: 50%;
            background: var(--blue);
            left: -22px;
            top: 4px;
        }

        .timeline-title {
            font-size: 11px;
            font-weight: 800;
        }

        .timeline-date {
            color: var(--muted2);
            font-size: 9px;
            margin-top: 3px;
        }

        pre.raw {
            max-height: 300px;
            overflow: auto;
            background: #080b10;
            border: 1px solid var(--border);
            border-radius: 10px;
            padding: 12px;
            color: #aab4c2;
            font-size: 10px;
            white-space: pre-wrap;
            word-break: break-word;
        }

        /* =========================
           EMPTY / LOADING
        ========================= */

        .loading,
        .empty {
            padding: 30px;
            text-align: center;
            color: var(--muted);
            font-size: 12px;
        }

        .spinner {
            width: 18px;
            height: 18px;
            border: 2px solid rgba(255,255,255,.15);
            border-top-color: var(--blue);
            border-radius: 50%;
            animation: spin .8s linear infinite;
            margin: 0 auto 9px;
        }

        @keyframes spin {
            to {
                transform: rotate(360deg);
            }
        }

        /* =========================
           RESPONSIVE
        ========================= */

        @media (max-width: 1100px) {
            .metrics {
                grid-template-columns: repeat(2,1fr);
            }

            .utm-grid {
                grid-template-columns: 1fr;
            }
        }

        @media (max-width: 780px) {
            :root {
                --sidebar: 0px;
            }

            .sidebar {
                display: none;
            }

            .main {
                margin-left: 0;
                padding: 20px 15px 130px;
            }

            .topbar {
                align-items: flex-start;
                flex-direction: column;
            }

            .metrics {
                grid-template-columns: 1fr 1fr;
            }

            .utm-stats {
                grid-template-columns: 1fr 1fr;
            }

            .utm-stat:nth-child(3) {
                border-left: 0;
                border-top: 1px solid var(--border);
            }

            .utm-stat:nth-child(4) {
                border-top: 1px solid var(--border);
            }

            .floating-utm {
                left: 12px;
                right: 12px;
                bottom: 12px;
                width: auto;
            }

            #toasts {
                left: 12px;
                right: 12px;
                width: auto;
                top: 12px;
            }

            .form-grid,
            .doc-grid {
                grid-template-columns: 1fr;
            }
        }

        @media (max-width: 480px) {
            .metrics {
                grid-template-columns: 1fr;
            }

            h1 {
                font-size: 25px;
            }
        }
    </style>
</head>

<body>

<div class="app">

    <!-- ================= SIDEBAR ================= -->

    <aside class="sidebar">

        <div class="brand">
            <div class="brand-logo">U</div>
            <div>
                <div class="brand-title">UTM Monitor</div>
                <div class="brand-subtitle">ЕГАИС • контроль УТМ</div>
            </div>
        </div>

        <div class="nav-title">Мониторинг</div>

        <nav class="nav">
            <button class="active" data-target="overview">
                <span class="nav-icon">⌂</span>
                Обзор
            </button>

            <button data-target="utms">
                <span class="nav-icon">◉</span>
                УТМ
            </button>

            <button data-target="ttn">
                <span class="nav-icon">▣</span>
                ТТН
            </button>

            <button data-target="problems">
                <span class="nav-icon">!</span>
                Проблемы
            </button>

            <button data-target="technical">
                <span class="nav-icon">⚙</span>
                Техническая информация
            </button>
        </nav>

    </aside>

    <!-- ================= MAIN ================= -->

    <main class="main">

        <header class="topbar">

            <div>
                <div class="eyebrow">ЕГАИС / УТМ</div>

                <h1>Центр контроля ЕГАИС</h1>

                <div class="subtitle">
                    Мониторинг УТМ, ТТН, маркированной продукции и сертификатов
                </div>
            </div>

            <div class="top-actions">

                <button class="btn" id="refreshBtn">
                    ↻ Обновить
                </button>

                <button class="btn" id="notificationsBtn">
                    🔔 Уведомления
                </button>

                <button class="btn btn-primary" id="addUtmBtn">
                    ＋ Добавить УТМ
                </button>

            </div>

        </header>


        <!-- ================= OVERVIEW ================= -->

        <section class="section" id="overview">

            <div class="hero">
                <h2>Система работает</h2>
                <p>
                    Контроль подключения УТМ, сертификатов RSA/GOST,
                    ТТН и сохранённых данных ЕГАИС.
                </p>
            </div>

            <div class="metrics">

                <div class="metric">
                    <div class="metric-head">
                        <span>УТМ</span>
                        <span class="metric-icon">◉</span>
                    </div>

                    <div class="metric-value" id="metricUtm">
                        —
                    </div>

                    <div class="metric-caption">
                        работающих подключений
                    </div>
                </div>


                <div class="metric">
                    <div class="metric-head">
                        <span>ТТН</span>
                        <span class="metric-icon">▣</span>
                    </div>

                    <div class="metric-value" id="metricTtn">
                        —
                    </div>

                    <div class="metric-caption">
                        документов в мониторинге
                    </div>
                </div>


                <div class="metric">
                    <div class="metric-head">
                        <span>Марки</span>
                        <span class="metric-icon">◇</span>
                    </div>

                    <div class="metric-value" id="metricMarks">
                        —
                    </div>

                    <div class="metric-caption">
                        сохранено в базе
                    </div>
                </div>


                <div class="metric">
                    <div class="metric-head">
                        <span>Проблемы</span>
                        <span class="metric-icon">!</span>
                    </div>

                    <div class="metric-value" id="metricProblems">
                        —
                    </div>

                    <div class="metric-caption">
                        требуют внимания
                    </div>
                </div>

            </div>

        </section>


        <!-- ================= UTM ================= -->

        <section class="section" id="utms">

            <div class="section-head">

                <div>
                    <div class="section-title">УТМ</div>
                    <div class="section-subtitle">
                        Состояние подключений и сертификатов
                    </div>
                </div>

                <div class="section-tools">
                    <span class="small" id="lastCheck">
                        Последняя проверка: —
                    </span>
                </div>

            </div>

            <div class="utm-grid" id="utmGrid">
                <div class="loading">
                    <div class="spinner"></div>
                    Проверяем УТМ...
                </div>
            </div>

        </section>


        <!-- ================= TTN ================= -->

        <section class="section" id="ttn">

            <div class="section-head">

                <div>
                    <div class="section-title">ТТН</div>
                    <div class="section-subtitle">
                        Входящие и исходящие документы
                    </div>
                </div>

                <div class="section-tools">
                    <button class="btn btn-small" id="refreshTtnBtn">
                        ↻ Обновить ТТН
                    </button>
                </div>

            </div>


            <div class="filters">

                <input
                    class="search"
                    id="ttnSearch"
                    type="search"
                    placeholder="Поиск по номеру, отправителю или получателю..."
                >

                <select id="utmFilter">
                    <option value="">Все УТМ</option>
                </select>

                <button class="filter active" data-direction="">
                    Все
                </button>

                <button class="filter" data-direction="in">
                    Входящие
                </button>

                <button class="filter" data-direction="out">
                    Исходящие
                </button>

            </div>


            <div class="table-wrap">

                <table>

                    <thead>
                        <tr>
                            <th>ТТН</th>
                            <th>Дата</th>
                            <th>Направление</th>
                            <th>Отправитель</th>
                            <th>Получатель</th>
                            <th>Товары</th>
                            <th>Марки</th>
                            <th>УТМ</th>
                            <th></th>
                        </tr>
                    </thead>

                    <tbody id="ttnTable">

                        <tr>
                            <td colspan="9">
                                <div class="loading">
                                    <div class="spinner"></div>
                                    Загружаем ТТН...
                                </div>
                            </td>
                        </tr>

                    </tbody>

                </table>

            </div>

        </section>


        <!-- ================= PROBLEMS ================= -->

        <section class="section" id="problems">

            <div class="section-head">

                <div>
                    <div class="section-title">
                        Проблемы и предупреждения
                    </div>

                    <div class="section-subtitle">
                        Состояние УТМ и сертификатов
                    </div>
                </div>

            </div>

            <div class="problem-box" id="problemsBox">

                <div class="problem-ok">

                    <div class="problem-ok-icon">
                        ✓
                    </div>

                    <div>
                        <strong>Проверяем состояние...</strong>

                        <div class="small">
                            Ожидаем данные мониторинга.
                        </div>
                    </div>

                </div>

            </div>

        </section>


        <!-- ================= TECHNICAL ================= -->

        <section class="section" id="technical">

            <div class="section-head">

                <div>
                    <div class="section-title">
                        Техническая информация
                    </div>

                    <div class="section-subtitle">
                        Служебные данные УТМ и ЕГАИС
                    </div>
                </div>

            </div>

            <details class="technical">

                <summary>
                    Технические документы ЕГАИС
                </summary>

                <div class="technical-content">

                    <p>
                        Служебные документы ЕГАИС не выводятся
                        в основном списке как самостоятельные бизнес-документы.
                    </p>

                    <p>
                        Внутри карточки ТТН доступны связанные документы:
                    </p>

                    <p>
                        <span class="tech-code">WayBill_v4</span>
                        <span class="tech-code">FORM2REGINFO</span>
                        <span class="tech-code">TTNHISTORYF2REG</span>
                        <span class="tech-code">ReplyRests_v3</span>
                        <span class="tech-code">ReplyRestsShop_v2</span>
                        <span class="tech-code">Ticket</span>
                    </p>

                    <p>
                        Это позволяет оставить основной интерфейс понятным
                        для пользователя, а технические данные сохранить
                        для диагностики.
                    </p>

                </div>

            </details>

        </section>


        <footer>
            UTM Monitor • автоматический контроль УТМ •
            <span id="footerTime">—</span>
        </footer>

    </main>

</div>


<!-- ================= FLOATING UTM ================= -->

<div class="floating-utm">

    <div class="floating-head">

        <span class="floating-title">
            УТМ
        </span>

        <span class="floating-total" id="floatingTotal">
            —
        </span>

    </div>

    <div id="floatingUtmList">
        <div class="loading">Загрузка...</div>
    </div>

    <button class="btn floating-add" id="floatingAddBtn">
        ＋ Добавить УТМ
    </button>

</div>


<!-- ================= TOASTS ================= -->

<div id="toasts"></div>


<!-- ================= ADD UTM MODAL ================= -->

<div class="modal-backdrop" id="addModal">

    <div class="modal small-modal">

        <div class="modal-head">

            <div class="modal-title">
                Добавить УТМ
            </div>

            <button class="modal-close" data-close="addModal">
                ×
            </button>

        </div>

        <form id="addUtmForm">

            <div class="modal-body">

                <div class="form-grid">

                    <div class="field full">
                        <label>Название магазина / УТМ</label>

                        <input
                            name="name"
                            required
                            placeholder="Например: Сникерс"
                        >
                    </div>

                    <div class="field">

                        <label>IP-адрес</label>

                        <input
                            name="ip"
                            required
                            placeholder="10.0.0.100"
                        >

                    </div>

                    <div class="field">

                        <label>Порт</label>

                        <input
                            name="port"
                            type="number"
                            min="1"
                            max="65535"
                            value="8086"
                            required
                        >

                    </div>

                </div>

            </div>

            <div class="modal-actions">

                <button
                    type="button"
                    class="btn"
                    data-close="addModal"
                >
                    Отмена
                </button>

                <button
                    type="submit"
                    class="btn btn-primary"
                >
                    Добавить УТМ
                </button>

            </div>

        </form>

    </div>

</div>


<!-- ================= DELETE MODAL ================= -->

<div class="modal-backdrop" id="deleteModal">

    <div class="modal small-modal">

        <div class="modal-head">

            <div class="modal-title">
                Удалить УТМ?
            </div>

            <button class="modal-close" data-close="deleteModal">
                ×
            </button>

        </div>

        <div class="modal-body">

            <div class="confirm-text" id="deleteText">
                УТМ будет удалён из мониторинга.
            </div>

        </div>

        <div class="modal-actions">

            <button
                class="btn"
                data-close="deleteModal"
            >
                Отмена
            </button>

            <button
                class="btn btn-danger"
                id="confirmDeleteBtn"
            >
                Удалить
            </button>

        </div>

    </div>

</div>


<!-- ================= DOCUMENT MODAL ================= -->

<div class="modal-backdrop" id="documentModal">

    <div class="modal document-modal">

        <div id="documentContent">

            <div class="loading">
                <div class="spinner"></div>
                Загружаем документ...
            </div>

        </div>

    </div>

</div>


<script>
'use strict';

/* =========================================================
   CONFIG
========================================================= */

const API = 'api.php';

const STATUS_REFRESH = 10000;
const DOCUMENT_REFRESH = 30000;

let statusData = [];
let documentsData = [];

let currentDirection = '';
let deleteUtmId = 0;

let statusTimer = null;
let documentTimer = null;

let notificationsEnabled =
    localStorage.getItem('utm_notifications') === '1';

let previousStatus =
    JSON.parse(
        localStorage.getItem('utm_previous_status') || '{}'
    );


/* =========================================================
   HELPERS
========================================================= */

function $(id) {
    return document.getElementById(id);
}

function escapeHtml(value) {

    if (value === null || value === undefined) {
        return '';
    }

    return String(value)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}


function formatDate(value) {

    if (!value) {
        return '—';
    }

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return String(value);
    }

    return date.toLocaleString('ru-RU');
}


function friendlyType(type) {

    const map = {
        'WayBill_v4': 'Товарно-транспортная накладная',
        'FORM2REGINFO': 'Регистрация формы 2',
        'TTNHISTORYF2REG': 'История ТТН',
        'ReplyRests_v3': 'Остатки ФСРАР',
        'ReplyRestsShop_v2': 'Остатки магазина',
        'Ticket': 'Квитанция ЕГАИС',
        'Documents': 'Документ'
    };

    return map[type] || type || 'Документ';
}


function friendlyStatus(status) {

    const value =
        String(status || '').toLowerCase();

    if (
        value.includes('accept') ||
        value.includes('success') ||
        value.includes('прин')
    ) {
        return 'Принято';
    }

    if (
        value.includes('reject') ||
        value.includes('откл')
    ) {
        return 'Отклонено';
    }

    if (
        value.includes('process') ||
        value.includes('wait') ||
        value.includes('pending')
    ) {
        return 'Обрабатывается';
    }

    if (value.includes('sent')) {
        return 'Отправлено';
    }

    return status || 'Не определён';
}


function statusClass(status) {

    const value =
        String(status || '').toLowerCase();

    if (
        value.includes('accept') ||
        value.includes('success') ||
        value.includes('прин')
    ) {
        return 'valid';
    }

    if (
        value.includes('reject') ||
        value.includes('откл')
    ) {
        return 'danger';
    }

    if (
        value.includes('process') ||
        value.includes('wait') ||
        value.includes('pending')
    ) {
        return 'warning';
    }

    return '';
}


function directionText(direction) {

    if (direction === 'in') {
        return 'Входящая';
    }

    if (direction === 'out') {
        return 'Исходящая';
    }

    return '—';
}


function showToast(
    title,
    text = '',
    type = 'success'
) {

    const container = $('toasts');

    const toast =
        document.createElement('div');

    toast.className =
        'toast ' + type;

    toast.innerHTML = `
        <div class="toast-title">
            ${escapeHtml(title)}
        </div>

        ${
            text
                ? `<div class="toast-text">${escapeHtml(text)}</div>`
                : ''
        }
    `;

    container.appendChild(toast);

    setTimeout(() => {
        toast.remove();
    }, 4500);
}


async function apiFetch(
    url,
    options = {}
) {

    const response =
        await fetch(url, {
            cache: 'no-store',
            ...options
        });

    const text =
        await response.text();

    let data;

    try {
        data = JSON.parse(text);
    } catch (e) {
        throw new Error(
            'API вернул некорректный JSON'
        );
    }

    if (!response.ok || data.success === false) {

        throw new Error(
            data.error ||
            `HTTP ${response.status}`
        );
    }

    return data;
}


/* =========================================================
   NAVIGATION
========================================================= */

document
    .querySelectorAll('.nav button')
    .forEach(button => {

        button.addEventListener(
            'click',
            () => {

                document
                    .querySelectorAll('.nav button')
                    .forEach(b =>
                        b.classList.remove('active')
                    );

                button.classList.add('active');

                const target =
                    $(button.dataset.target);

                if (target) {
                    target.scrollIntoView({
                        behavior: 'smooth',
                        block: 'start'
                    });
                }
            }
        );

    });


/* =========================================================
   STATUS
========================================================= */

async function loadStatus(
    silent = false
) {

    try {

        const data =
            await apiFetch(
                API + '?action=status&_=' + Date.now()
            );

        const oldStatus =
            previousStatus;

        statusData =
            Array.isArray(data.utms)
                ? data.utms
                : [];

        renderStatus(data.server_time);

        detectStatusChanges(oldStatus);

        previousStatus = {};

        statusData.forEach(utm => {

            previousStatus[utm.id] = {
                online: !!utm.online,
                rsa: utm.rsa_info?.status || null,
                gost: utm.gost_info?.status || null
            };

        });

        localStorage.setItem(
            'utm_previous_status',
            JSON.stringify(previousStatus)
        );

    } catch (error) {

        if (!silent) {

            showToast(
                'Ошибка мониторинга',
                error.message,
                'error'
            );
        }
    }

}


function renderStatus(serverTime) {

    const total =
        statusData.length;

    const online =
        statusData.filter(
            x => x.online
        ).length;

    let problems = 0;

    statusData.forEach(utm => {

        if (!utm.online) {
            problems++;
        }

        if (
            utm.rsa_info &&
            ['expired','invalid','critical','warning']
                .includes(utm.rsa_info.status)
        ) {
            problems++;
        }

        if (
            utm.gost_info &&
            ['expired','invalid','critical','warning']
                .includes(utm.gost_info.status)
        ) {
            problems++;
        }

        if (
            Array.isArray(utm.warnings)
        ) {
            problems +=
                utm.warnings.length;
        }

    });

    $('metricUtm').textContent =
        `${online}/${total}`;

    $('metricProblems').textContent =
        problems;

    const marks =
        calculateMarks();

    $('metricMarks').textContent =
        marks.toLocaleString('ru-RU');

    const ttnCount =
        documentsData.filter(
            isBusinessDocument
        ).length;

    $('metricTtn').textContent =
        ttnCount;

    $('lastCheck').textContent =
        'Последняя проверка: ' +
        (
            serverTime ||
            new Date().toLocaleString('ru-RU')
        );

    $('footerTime').textContent =
        new Date().toLocaleString('ru-RU');

    renderUtmCards();
    renderFloatingUtm();
    renderProblems();

    updateUtmFilter();
}


function calculateMarks() {

    return documentsData.reduce(
        (sum, doc) => {

            const count =
                Number(doc.marks_count || 0);

            return sum + count;

        },
        0
    );
}


function renderUtmCards() {

    const container =
        $('utmGrid');

    if (!statusData.length) {

        container.innerHTML = `
            <div class="empty">
                УТМ не добавлены.
            </div>
        `;

        return;
    }

    container.innerHTML =
        statusData.map(
            utm => {

                const online =
                    !!utm.online;

                const rsaDays =
                    utm.rsa_info?.days_left;

                const gostDays =
                    utm.gost_info?.days_left;

                return `
                    <article
                        class="utm-card ${online ? 'online' : 'offline'}"
                    >

                        <div class="utm-card-top">

                            <div>
                                <div class="utm-name">
                                    ${escapeHtml(utm.name)}
                                </div>

                                <div class="utm-address">
                                    ${escapeHtml(utm.ip)}:${escapeHtml(utm.port)}
                                </div>
                            </div>

                            <span
                                class="status-pill ${
                                    online
                                        ? 'online'
                                        : 'offline'
                                }"
                            >
                                <span class="dot"></span>

                                ${
                                    online
                                        ? 'ONLINE'
                                        : 'OFFLINE'
                                }
                            </span>

                        </div>


                        <div class="utm-stats">

                            <div class="utm-stat">

                                <div class="utm-stat-label">
                                    Версия
                                </div>

                                <div class="utm-stat-value">
                                    ${escapeHtml(utm.version || '—')}
                                </div>

                            </div>


                            <div class="utm-stat">

                                <div class="utm-stat-label">
                                    RSA
                                </div>

                                <div class="utm-stat-value ${
                                    rsaDays !== null &&
                                    rsaDays !== undefined &&
                                    rsaDays <= 30
                                        ? 'warning'
                                        : 'valid'
                                }">

                                    ${
                                        utm.rsa_info?.is_valid === false
                                            ? '✕'
                                            : '✓'
                                    }

                                    ${
                                        rsaDays !== null &&
                                        rsaDays !== undefined
                                            ? ` ${rsaDays} дн.`
                                            : ''
                                    }

                                </div>

                            </div>


                            <div class="utm-stat">

                                <div class="utm-stat-label">
                                    GOST
                                </div>

                                <div class="utm-stat-value ${
                                    gostDays !== null &&
                                    gostDays !== undefined &&
                                    gostDays <= 30
                                        ? 'warning'
                                        : 'valid'
                                }">

                                    ${
                                        utm.gost_info?.is_valid === false
                                            ? '✕'
                                            : '✓'
                                    }

                                    ${
                                        gostDays !== null &&
                                        gostDays !== undefined
                                            ? ` ${gostDays} дн.`
                                            : ''
                                    }

                                </div>

                            </div>


                            <div class="utm-stat">

                                <div class="utm-stat-label">
                                    Ответ
                                </div>

                                <div class="utm-stat-value">
                                    ${escapeHtml(utm.response_time)} мс
                                </div>

                            </div>

                        </div>


                        <div class="utm-card-actions">

                            <button
                                class="btn btn-small"
                                onclick="checkSingleUtm(${utm.id})"
                            >
                                Проверить
                            </button>

                            <button
                                class="btn btn-small"
                                onclick="showCertificates(${utm.id})"
                            >
                                Сертификаты
                            </button>

                            <button
                                class="btn btn-small"
                                onclick="showUtmInfo(${utm.id})"
                            >
                                Настроить
                            </button>

                            <button
                                class="btn btn-small btn-danger"
                                onclick="askDeleteUtm(
                                    ${utm.id},
                                    '${escapeHtml(utm.name)}'
                                )"
                            >
                                Удалить
                            </button>

                        </div>

                    </article>
                `;
            }
        )
        .join('');
}


/* =========================================================
   FLOATING UTM
========================================================= */

function renderFloatingUtm() {

    const container =
        $('floatingUtmList');

    const online =
        statusData.filter(
            x => x.online
        ).length;

    $('floatingTotal').textContent =
        `${online}/${statusData.length} ONLINE`;

    if (!statusData.length) {

        container.innerHTML = `
            <div class="empty">
                Нет УТМ
            </div>
        `;

        return;
    }

    container.innerHTML =
        statusData.map(
            utm => {

                const online =
                    !!utm.online;

                return `
                    <div
                        class="floating-item"
                        onclick="focusUtm()"
                    >

                        <div class="floating-left">

                            <div class="floating-name">
                                ${escapeHtml(utm.name)}
                            </div>

                            <div class="floating-address">
                                ${escapeHtml(utm.ip)}:${escapeHtml(utm.port)}
                            </div>

                        </div>

                        <div
                            class="floating-status ${
                                online
                                    ? 'online'
                                    : 'offline'
                            }"
                        >

                            <span class="dot"></span>

                            ${
                                online
                                    ? 'ONLINE'
                                    : 'OFFLINE'
                            }

                        </div>

                    </div>
                `;
            }
        )
        .join('');
}


function focusUtm() {

    $('utms').scrollIntoView({
        behavior: 'smooth'
    });

}


$('floatingAddBtn')
    .addEventListener(
        'click',
        openAddUtm
    );


/* =========================================================
   STATUS CHANGES / NOTIFICATIONS
========================================================= */

function detectStatusChanges(oldStatus) {

    if (!Object.keys(oldStatus).length) {
        return;
    }

    statusData.forEach(utm => {

        const old =
            oldStatus[utm.id];

        if (!old) {
            return;
        }

        if (
            old.online !==
            !!utm.online
        ) {

            if (utm.online) {

                notify(
                    'УТМ снова доступен',
                    `${utm.name} снова ONLINE`,
                    'success'
                );

            } else {

                notify(
                    'УТМ недоступен',
                    `${utm.name} перешёл в OFFLINE`,
                    'error'
                );

            }

        }

        const rsaStatus =
            utm.rsa_info?.status || null;

        if (
            old.rsa &&
            old.rsa !== rsaStatus &&
            ['warning','critical','expired','invalid']
                .includes(rsaStatus)
        ) {

            notify(
                'Проблема RSA',
                `${utm.name}: сертификат требует внимания`,
                'warning'
            );

        }

        const gostStatus =
            utm.gost_info?.status || null;

        if (
            old.gost &&
            old.gost !== gostStatus &&
            ['warning','critical','expired','invalid']
                .includes(gostStatus)
        ) {

            notify(
                'Проблема GOST',
                `${utm.name}: сертификат требует внимания`,
                'warning'
            );

        }

    });

}


function notify(
    title,
    text,
    type
) {

    showToast(
        title,
        text,
        type
    );

    if (
        notificationsEnabled &&
        'Notification' in window &&
        Notification.permission === 'granted'
    ) {

        try {
            new Notification(
                title,
                {
                    body: text
                }
            );
        } catch (e) {}
    }

}


/* =========================================================
   PROBLEMS
========================================================= */

function renderProblems() {

    const container =
        $('problemsBox');

    const problems = [];

    statusData.forEach(utm => {

        if (!utm.online) {

            problems.push({
                level: 'critical',
                text:
                    `${utm.name}: УТМ недоступен`
            });

        }

        [
            ['RSA', utm.rsa_info],
            ['GOST', utm.gost_info]
        ].forEach(
            ([type, cert]) => {

                if (!cert) {
                    return;
                }

                if (
                    ['expired','invalid','critical']
                        .includes(cert.status)
                ) {

                    problems.push({
                        level: 'critical',
                        text:
                            `${utm.name}: ${type} — ${cert.status_text}`
                    });

                } else if (
                    cert.status === 'warning'
                ) {

                    problems.push({
                        level: 'warning',
                        text:
                            `${utm.name}: ${type} — осталось ${cert.days_left} дн.`
                    });

                }

            }
        );

    });


    if (!problems.length) {

        container.innerHTML = `
            <div class="problem-ok">

                <div class="problem-ok-icon">
                    ✓
                </div>

                <div>
                    <strong>
                        Всё в порядке
                    </strong>

                    <div class="small">
                        Критических проблем не обнаружено.
                    </div>
                </div>

            </div>
        `;

        return;
    }


    container.innerHTML =
        problems.map(
            problem => `
                <div
                    style="
                        padding:10px 0;
                        border-bottom:1px solid var(--border);
                        color:${
                            problem.level === 'critical'
                                ? 'var(--red)'
                                : 'var(--yellow)'
                        };
                        font-size:12px;
                    "
                >
                    ${
                        problem.level === 'critical'
                            ? '●'
                            : '▲'
                    }

                    ${escapeHtml(problem.text)}
                </div>
            `
        )
        .join('');
}


/* =========================================================
   UTM FILTER
========================================================= */

function updateUtmFilter() {

    const select =
        $('utmFilter');

    const current =
        select.value;

    select.innerHTML =
        '<option value="">Все УТМ</option>' +
        statusData.map(
            utm =>
                `<option value="${utm.id}">
                    ${escapeHtml(utm.name)}
                </option>`
        ).join('');

    if (
        [...select.options]
            .some(o => o.value === current)
    ) {
        select.value = current;
    }

}


/* =========================================================
   DOCUMENTS
========================================================= */

function isBusinessDocument(doc) {

    const type =
        String(doc.document_type || '');

    return type === 'WayBill_v4';
}


async function loadDocuments(
    silent = false
) {

    try {

        const params =
            new URLSearchParams();

        params.set(
            'action',
            'documents'
        );

        params.set(
            'limit',
            '500'
        );

        const utmId =
            $('utmFilter').value;

        if (utmId) {
            params.set(
                'utm_id',
                utmId
            );
        }

        if (currentDirection) {

            params.set(
                'direction',
                currentDirection
            );

        }

        const q =
            $('ttnSearch').value.trim();

        if (q) {
            params.set('q', q);
        }

        params.set(
            '_',
            Date.now()
        );

        const data =
            await apiFetch(
                API + '?' + params.toString()
            );

        documentsData =
            Array.isArray(data.documents)
                ? data.documents
                : [];

        renderTtn();

        const marks =
            calculateMarks();

        $('metricMarks').textContent =
            marks.toLocaleString('ru-RU');

        const count =
            documentsData.filter(
                isBusinessDocument
            ).length;

        $('metricTtn').textContent =
            count;

    } catch (error) {

        if (!silent) {

            showToast(
                'Ошибка загрузки ТТН',
                error.message,
                'error'
            );

        }

    }

}


function renderTtn() {

    const tbody =
        $('ttnTable');

    const documents =
        documentsData.filter(
            isBusinessDocument
        );

    if (!documents.length) {

        tbody.innerHTML = `
            <tr>
                <td colspan="9">
                    <div class="empty">
                        ТТН не найдены.
                    </div>
                </td>
            </tr>
        `;

        return;
    }


    tbody.innerHTML =
        documents.map(
            doc => {

                const sender =
                    doc.sender_name ||
                    '—';

                const receiver =
                    doc.receiver_name ||
                    '—';

                const items =
                    Number(
                        doc.items_count || 0
                    );

                const marks =
                    Number(
                        doc.marks_count || 0
                    );

                return `
                    <tr>

                        <td>
                            <div class="ttn-number">
                                № ${escapeHtml(
                                    doc.number || '—'
                                )}
                            </div>

                            <div class="small">
                                ${escapeHtml(
                                    friendlyStatus(doc.status)
                                )}
                            </div>
                        </td>


                        <td>
                            ${escapeHtml(
                                formatDate(
                                    doc.document_date
                                )
                            )}
                        </td>


                        <td>

                            <span
                                class="direction ${
                                    doc.direction === 'in'
                                        ? 'in'
                                        : 'out'
                                }"
                            >
                                ${escapeHtml(
                                    directionText(
                                        doc.direction
                                    )
                                )}
                            </span>

                        </td>


                        <td>
                            <div>
                                ${escapeHtml(sender)}
                            </div>
                        </td>


                        <td>
                            <div>
                                ${escapeHtml(receiver)}
                            </div>
                        </td>


                        <td>
                            <strong>
                                ${items}
                            </strong>
                        </td>


                        <td>
                            <span class="mark-count">
                                ${marks}
                            </span>
                        </td>


                        <td>
                            ${escapeHtml(
                                doc.utm_name || '—'
                            )}
                        </td>


                        <td>
                            <button
                                class="btn btn-small"
                                onclick="openDocument(
                                    ${Number(doc.id)}
                                )"
                            >
                                Открыть
                            </button>
                        </td>

                    </tr>
                `;

            }
        )
        .join('');
}


/* =========================================================
   DOCUMENT DETAIL
========================================================= */

async function openDocument(id) {

    $('documentModal')
        .classList.add('open');

    $('documentContent').innerHTML = `
        <div class="loading">
            <div class="spinner"></div>
            Загружаем ТТН...
        </div>
    `;

    try {

        const data =
            await apiFetch(
                API +
                '?action=document&id=' +
                encodeURIComponent(id)
            );

        renderDocument(data);

    } catch (error) {

        $('documentContent').innerHTML = `
            <div class="loading">
                Не удалось загрузить документ.
                <br><br>
                ${escapeHtml(error.message)}
            </div>
        `;

    }

}


function renderDocument(data) {

    const doc =
        data.document ||
        data;

    const items =
        Array.isArray(data.items)
            ? data.items
            : [];

    const history =
        Array.isArray(data.history)
            ? data.history
            : [];

    const links =
        Array.isArray(data.links)
            ? data.links
            : [];

    const marks =
        items.flatMap(
            item =>
                Array.isArray(item.marks)
                    ? item.marks
                    : []
        );


    const sender =
        doc.sender_name ||
        '—';

    const receiver =
        doc.receiver_name ||
        '—';


    $('documentContent').innerHTML = `

        <div class="modal-head">

            <div class="modal-title">
                ТТН / ЕГАИС
            </div>

            <button
                class="modal-close"
                data-close="documentModal"
            >
                ×
            </button>

        </div>


        <div class="document-header">

            <div>

                <div class="document-number">
                    ТТН №${escapeHtml(
                        doc.number || '—'
                    )}
                </div>

                <div class="document-meta">

                    ${
                        escapeHtml(
                            formatDate(
                                doc.document_date
                            )
                        )
                    }

                    •

                    ${
                        escapeHtml(
                            doc.utm_name || '—'
                        )
                    }

                </div>

            </div>


            <div class="doc-status">

                <span class="status-pill online">

                    ${escapeHtml(
                        friendlyStatus(
                            doc.status
                        )
                    )}

                </span>

            </div>

        </div>


        <div class="doc-grid">

            <div class="doc-info-card">

                <div class="doc-info-title">
                    Отправитель
                </div>

                <div class="doc-info-value">
                    ${escapeHtml(sender)}
                </div>

            </div>


            <div class="doc-info-card">

                <div class="doc-info-title">
                    Получатель
                </div>

                <div class="doc-info-value">
                    ${escapeHtml(receiver)}
                </div>

            </div>


            <div class="doc-info-card">

                <div class="doc-info-title">
                    Направление
                </div>

                <div class="doc-info-value">
                    ${escapeHtml(
                        directionText(
                            doc.direction
                        )
                    )}
                </div>

            </div>


            <div class="doc-info-card">

                <div class="doc-info-title">
                    Марки
                </div>

                <div class="doc-info-value">
                    <strong class="mark-count">
                        ${marks.length}
                    </strong>
                </div>

            </div>

        </div>


        <div class="doc-content">

            <div class="doc-section-title">
                Товары
            </div>

            <div class="table-wrap">

                <table>

                    <thead>

                        <tr>
                            <th>#</th>
                            <th>Товар</th>
                            <th>Количество</th>
                            <th>Марки</th>
                        </tr>

                    </thead>

                    <tbody>

                        ${
                            items.length
                                ? items.map(
                                    (item,index) => `

                                        <tr>

                                            <td>
                                                ${index + 1}
                                            </td>

                                            <td>
                                                ${
                                                    escapeHtml(
                                                        item.product_name ||
                                                        item.full_name ||
                                                        item.name ||
                                                        'Товар'
                                                    )
                                                }
                                            </td>

                                            <td>
                                                ${
                                                    escapeHtml(
                                                        item.quantity ||
                                                        '—'
                                                    )
                                                }
                                            </td>

                                            <td>
                                                <span class="mark-count">
                                                    ${
                                                        Array.isArray(item.marks)
                                                            ? item.marks.length
                                                            : Number(
                                                                item.marks_count || 0
                                                            )
                                                    }
                                                </span>
                                            </td>

                                        </tr>

                                    `
                                ).join('')
                                : `
                                    <tr>
                                        <td colspan="4">
                                            <div class="empty">
                                                Товары не найдены.
                                            </div>
                                        </td>
                                    </tr>
                                `
                        }

                    </tbody>

                </table>

            </div>


            <div class="doc-section-title">
                Маркированная продукция
            </div>

            <div class="marks">

                ${
                    marks.length
                        ? marks.map(
                            mark => `
                                <span class="mark">
                                    ${escapeHtml(
                                        typeof mark === 'string'
                                            ? mark
                                            : mark.amc || mark.code || ''
                                    )}
                                </span>
                            `
                        ).join('')
                        : `
                            <span class="small">
                                Марки отсутствуют.
                            </span>
                        `
                }

            </div>


            ${
                history.length
                    ? `

                        <div class="doc-section-title">
                            История документа
                        </div>

                        <div class="timeline">

                            ${
                                history.map(
                                    event => `

                                        <div class="timeline-item">

                                            <div class="timeline-title">
                                                ${
                                                    escapeHtml(
                                                        event.status ||
                                                        event.status_text ||
                                                        'Изменение состояния'
                                                    )
                                                }
                                            </div>

                                            <div class="timeline-date">
                                                ${
                                                    escapeHtml(
                                                        formatDate(
                                                            event.created_at ||
                                                            event.event_date
                                                        )
                                                    )
                                                }
                                            </div>

                                        </div>

                                    `
                                ).join('')
                            }

                        </div>

                    `
                    : ''
            }


            ${
                links.length
                    ? `

                        <div class="doc-section-title">
                            Связанные документы ЕГАИС
                        </div>

                        <div class="table-wrap">

                            <table>

                                <thead>

                                    <tr>
                                        <th>Тип</th>
                                        <th>Номер</th>
                                        <th>Статус</th>
                                    </tr>

                                </thead>

                                <tbody>

                                    ${
                                        links.map(
                                            link => `

                                                <tr>

                                                    <td>
                                                        ${
                                                            escapeHtml(
                                                                friendlyType(
                                                                    link.type ||
                                                                    link.document_type
                                                                )
                                                            )
                                                        }
                                                    </td>

                                                    <td>
                                                        ${
                                                            escapeHtml(
                                                                link.number ||
                                                                '—'
                                                            )
                                                        }
                                                    </td>

                                                    <td>
                                                        ${
                                                            escapeHtml(
                                                                friendlyStatus(
                                                                    link.status
                                                                )
                                                            )
                                                        }
                                                    </td>

                                                </tr>

                                            `
                                        ).join('')
                                    }

                                </tbody>

                            </table>

                        </div>

                    `
                    : ''
            }


            <details class="technical" style="margin-top:18px">

                <summary>
                    Техническая информация
                </summary>

                <div class="technical-content">

                    <p>
                        Тип документа:
                        <span class="tech-code">
                            ${escapeHtml(
                                doc.document_type
                            )}
                        </span>
                    </p>

                    ${
                        doc.external_document_id
                            ? `
                                <p>
                                    ID:
                                    <span class="tech-code">
                                        ${escapeHtml(
                                            doc.external_document_id
                                        )}
                                    </span>
                                </p>
                            `
                            : ''
                    }

                    ${
                        doc.raw_xml
                            ? `
                                <pre class="raw">${escapeHtml(
                                    doc.raw_xml
                                )}</pre>
                            `
                            : ''
                    }

                </div>

            </details>

        </div>
    `;

}


/* =========================================================
   UTM ACTIONS
========================================================= */

async function checkSingleUtm(id) {

    showToast(
        'Проверка УТМ',
        'Проверяем подключение...',
        'success'
    );

    await loadStatus(false);

    const utm =
        statusData.find(
            x => Number(x.id) === Number(id)
        );

    if (!utm) {
        return;
    }

    if (utm.online) {

        showToast(
            'УТМ доступен',
            `${utm.name}: ONLINE, ответ ${utm.response_time} мс`,
            'success'
        );

    } else {

        showToast(
            'УТМ недоступен',
            `${utm.name}: ${utm.error || 'нет соединения'}`,
            'error'
        );

    }

}


async function showCertificates(id) {

    try {

        const data =
            await apiFetch(
                API +
                '?action=certificates&id=' +
                encodeURIComponent(id)
            );

        const rsa =
            data.rsa?.info;

        const gost =
            data.gost?.info;

        showToast(
            'Сертификаты',
            `RSA: ${rsa?.status_text || '—'} • GOST: ${gost?.status_text || '—'}`,
            (
                rsa?.status === 'valid' &&
                gost?.status === 'valid'
            )
                ? 'success'
                : 'warning'
        );

    } catch (error) {

        showToast(
            'Ошибка сертификатов',
            error.message,
            'error'
        );

    }

}


async function showUtmInfo(id) {

    try {

        const data =
            await apiFetch(
                API +
                '?action=info&id=' +
                encodeURIComponent(id)
            );

        const info =
            data.data || {};

        const text =
            `Версия ${info.version || '—'}, ` +
            `RSA ${info.rsa ? 'есть' : '—'}, ` +
            `GOST ${info.gost ? 'есть' : '—'}`;

        showToast(
            'Информация УТМ',
            text,
            'success'
        );

    } catch (error) {

        showToast(
            'Ошибка',
            error.message,
            'error'
        );

    }

}


/* =========================================================
   ADD UTM
========================================================= */

function openAddUtm() {

    $('addModal')
        .classList.add('open');

}


$('addUtmBtn')
    .addEventListener(
        'click',
        openAddUtm
    );


$('addUtmForm')
    .addEventListener(
        'submit',
        async event => {

            event.preventDefault();

            const form =
                event.target;

            const formData =
                new FormData(form);

            const payload = {

                name:
                    formData.get('name'),

                ip:
                    formData.get('ip'),

                port:
                    Number(
                        formData.get('port')
                    )

            };


            try {

                await apiFetch(
                    API + '?action=add_utm',
                    {
                        method: 'POST',

                        headers: {
                            'Content-Type':
                                'application/json'
                        },

                        body:
                            JSON.stringify(
                                payload
                            )
                    }
                );


                form.reset();

                $('addModal')
                    .classList.remove('open');

                showToast(
                    'УТМ добавлен',
                    `${payload.name} добавлен в мониторинг`,
                    'success'
                );


                await loadStatus();

                await loadDocuments(true);

            } catch (error) {

                showToast(
                    'Не удалось добавить УТМ',
                    error.message,
                    'error'
                );

            }

        }
    );


/* =========================================================
   DELETE UTM
========================================================= */

function askDeleteUtm(
    id,
    name
) {

    deleteUtmId =
        Number(id);

    $('deleteText').textContent =
        `УТМ «${name}» будет удалён из мониторинга вместе с его настройкой подключения.`;

    $('deleteModal')
        .classList.add('open');

}


$('confirmDeleteBtn')
    .addEventListener(
        'click',
        async () => {

            if (!deleteUtmId) {
                return;
            }

            try {

                await apiFetch(
                    API + '?action=delete_utm',
                    {
                        method: 'POST',

                        headers: {
                            'Content-Type':
                                'application/json'
                        },

                        body:
                            JSON.stringify({
                                id: deleteUtmId
                            })
                    }
                );


                $('deleteModal')
                    .classList.remove('open');

                showToast(
                    'УТМ удалён',
                    'Подключение удалено из мониторинга.',
                    'success'
                );

                deleteUtmId = 0;

                await loadStatus();

                await loadDocuments(true);

            } catch (error) {

                showToast(
                    'Ошибка удаления',
                    error.message,
                    'error'
                );

            }

        }
    );


/* =========================================================
   MODALS CLOSE
========================================================= */

document
    .querySelectorAll('[data-close]')
    .forEach(button => {

        button.addEventListener(
            'click',
            () => {

                const id =
                    button.dataset.close;

                $(id)
                    ?.classList
                    .remove('open');

            }
        );

    });


document
    .querySelectorAll('.modal-backdrop')
    .forEach(backdrop => {

        backdrop.addEventListener(
            'click',
            event => {

                if (
                    event.target === backdrop
                ) {
                    backdrop.classList.remove('open');
                }

            }
        );

    });


document
    .addEventListener(
        'keydown',
        event => {

            if (
                event.key === 'Escape'
            ) {

                document
                    .querySelectorAll(
                        '.modal-backdrop.open'
                    )
                    .forEach(
                        modal =>
                            modal.classList.remove(
                                'open'
                            )
                    );

            }

        }
    );


/* =========================================================
   REFRESH
========================================================= */

$('refreshBtn')
    .addEventListener(
        'click',
        async () => {

            await loadStatus();

            await loadDocuments();

            showToast(
                'Обновлено',
                'Состояние УТМ и документы обновлены.',
                'success'
            );

        }
    );


$('refreshTtnBtn')
    .addEventListener(
        'click',
        () => loadDocuments()
    );


/* =========================================================
   FILTERS
========================================================= */

document
    .querySelectorAll(
        '.filter[data-direction]'
    )
    .forEach(button => {

        button.addEventListener(
            'click',
            () => {

                document
                    .querySelectorAll(
                        '.filter[data-direction]'
                    )
                    .forEach(
                        b =>
                            b.classList.remove(
                                'active'
                            )
                    );

                button.classList.add('active');

                currentDirection =
                    button.dataset.direction;

                loadDocuments();

            }
        );

    });


let searchTimer = null;

$('ttnSearch')
    .addEventListener(
        'input',
        () => {

            clearTimeout(
                searchTimer
            );

            searchTimer =
                setTimeout(
                    () => loadDocuments(true),
                    350
                );

        }
    );


$('utmFilter')
    .addEventListener(
        'change',
        () => loadDocuments()
    );


/* =========================================================
   NOTIFICATIONS
========================================================= */

$('notificationsBtn')
    .addEventListener(
        'click',
        async () => {

            if (
                !('Notification' in window)
            ) {

                showToast(
                    'Уведомления недоступны',
                    'Браузер не поддерживает системные уведомления.',
                    'warning'
                );

                return;
            }


            if (
                Notification.permission ===
                'default'
            ) {

                const permission =
                    await Notification.requestPermission();

                if (
                    permission !==
                    'granted'
                ) {

                    showToast(
                        'Уведомления отключены',
                        'Разрешение браузера не получено.',
                        'warning'
                    );

                    return;
                }

            }


            notificationsEnabled =
                !notificationsEnabled;

            localStorage.setItem(
                'utm_notifications',
                notificationsEnabled
                    ? '1'
                    : '0'
            );


            showToast(
                notificationsEnabled
                    ? 'Уведомления включены'
                    : 'Уведомления отключены',

                notificationsEnabled
                    ? 'Будем сообщать о смене состояния УТМ и сертификатов.'
                    : 'Системные уведомления отключены.',

                'success'
            );

        }
    );


/* =========================================================
   AUTO REFRESH
========================================================= */

function startAutoRefresh() {

    clearInterval(
        statusTimer
    );

    clearInterval(
        documentTimer
    );


    statusTimer =
        setInterval(
            () => loadStatus(true),
            STATUS_REFRESH
        );


    documentTimer =
        setInterval(
            () => loadDocuments(true),
            DOCUMENT_REFRESH
        );

}


/* =========================================================
   INIT
========================================================= */

async function init() {

    await loadStatus(true);

    await loadDocuments(true);

    startAutoRefresh();

}


init();

</script>

</body>
</html>