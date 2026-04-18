// ─── Controller changes needed to use the shared views ────────────────────────
//
// 1. Both GET confirm actions return the shared view:
//
//    ConfirmNowPaymentsAsync GET  →  return this.View("ConfirmCheckout", vm);
//    ConfirmBtcPayAsync GET       →  return this.View("ConfirmCheckout", vm);
//
// 2. Both success actions return the shared view:
//
//    NowPaymentsSuccess  →  return this.View("PaymentSuccess", viewModel);
//    BtcPaySuccess       →  return this.View("PaymentSuccess", viewModel);
//
// 3. Both POST confirm actions accept the new captcha context.
//    Change the fallback default in each from their old context name to:
//
//    ctx = "sponsoredlisting-confirmcheckout";
//
//    The CaptchaContext hidden field in the view already sends this value,
//    so the fallback only triggers if the field is somehow missing.
//
// 4. The captcha CAPTCHA validation in ConfirmedNowPaymentsAsync also has
//    a path that re-renders the view on failure. That path must also use:
//
//    return this.View("ConfirmCheckout", vmFail);
//
//    Same for ConfirmedBtcPayAsync on captcha/email failure.
