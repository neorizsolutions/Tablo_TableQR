export interface PublicPricingPlan {
  id: string;
  name: string;
  badge: string;
  highlight: boolean;
  firstYearPriceInr: number;
  annualRenewalPriceInr: number;
  setupFeeInr: number;
  description: string;
  features: string[];
}

export const ONE_TIME_SETUP_FEE_INR = 2000;

export const publicPricingPlans: PublicPricingPlan[] = [
  {
    id: "starter",
    name: "Starter",
    badge: "Essential",
    highlight: false,
    firstYearPriceInr: 4990,
    annualRenewalPriceInr: 2990,
    setupFeeInr: ONE_TIME_SETUP_FEE_INR,
    description: "Essential QR menu & kitchen display for independent cafes and quick-service diners.",
    features: [
      "Customer QR menu & self-ordering",
      "Live kitchen display & preparation queues",
      "Standard billing & cash settlement",
      "Basic daily sales & item reports",
      "Bilingual English & Malayalam menu",
    ],
  },
  {
    id: "standard",
    name: "Standard",
    badge: "Floor Operations",
    highlight: false,
    firstYearPriceInr: 5999,
    annualRenewalPriceInr: 3999,
    setupFeeInr: ONE_TIME_SETUP_FEE_INR,
    description: "Full service floor operations with waiter ordering, split bills, and table management.",
    features: [
      "Everything in Starter",
      "Staff & waiter table ordering",
      "Dining groups & shared bill splitting",
      "Mixed payments (Cash, UPI, Card)",
      "Table transfers & live table recovery",
      "Daily order & payment reports",
    ],
  },
  {
    id: "business",
    name: "Business",
    badge: "Most Popular",
    highlight: true,
    firstYearPriceInr: 7200,
    annualRenewalPriceInr: 5200,
    setupFeeInr: ONE_TIME_SETUP_FEE_INR,
    description: "Core inventory management, recipe costing, yield tracking, and stock reconciliation.",
    features: [
      "Everything in Standard",
      "Ingredient stock & recipe costing",
      "Thalassery biryani yield calculation",
      "Automatic stock deductions on bill settlement",
      "End-of-day stocktakes & variance reconciliation",
      "Advanced payment & reconciliation reports",
    ],
  },
  {
    id: "premium",
    name: "Premium",
    badge: "Enterprise",
    highlight: false,
    firstYearPriceInr: 8990,
    annualRenewalPriceInr: 6990,
    setupFeeInr: ONE_TIME_SETUP_FEE_INR,
    description: "Complete restaurant ERP: multi-outlet transfers, purchasing, and gross profit reports.",
    features: [
      "Everything in Business",
      "Purchase orders, goods receiving & invoices",
      "Supplier management & purchase returns",
      "Semi-finished batch preparations",
      "Stock lots & expiry date tracking",
      "Multi-outlet stock transfers",
      "Sales, stock valuation & gross profit reports",
    ],
  },
];
