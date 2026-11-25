-- BudgetSuite Central MariaDB Schema
CREATE DATABASE IF NOT EXISTS `budgetsuite` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `budgetsuite`;

-- 1. Wallets & Balance State Table
CREATE TABLE IF NOT EXISTS `wallets` (
  `id` INT PRIMARY KEY DEFAULT 1,
  `current_bank` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  `target_balance` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  `naver_points` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  `kakao_pay_money` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  `hold_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  `updated_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 2. Transactions History Table (지출 & 수입)
CREATE TABLE IF NOT EXISTS `transactions` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `tx_date` DATE NOT NULL,
  `tx_time` VARCHAR(10) NOT NULL,
  `item_type` VARCHAR(20) NOT NULL DEFAULT '지출', -- '지출' or '수입'
  `description` VARCHAR(255) NOT NULL,
  `amount` DECIMAL(12,2) NOT NULL,
  `payment_method` VARCHAR(50) NOT NULL DEFAULT '현금/체크',
  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_date (`tx_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 3. Monthly Fixed Expenses Checklist Table
CREATE TABLE IF NOT EXISTS `fixed_expenses` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `name` VARCHAR(100) NOT NULL,
  `amount` DECIMAL(12,2) NOT NULL,
  `is_paid` TINYINT(1) NOT NULL DEFAULT 0,
  `active_month` VARCHAR(7) NOT NULL, -- e.g. '2026-10'
  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 4. Monthly Plans Table (수당/수입 & 투자/저축 계획)
CREATE TABLE IF NOT EXISTS `plans` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `plan_type` VARCHAR(20) NOT NULL, -- 'INCOME' or 'INVEST'
  `name` VARCHAR(100) NOT NULL,
  `amount` DECIMAL(12,2) NOT NULL,
  `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
