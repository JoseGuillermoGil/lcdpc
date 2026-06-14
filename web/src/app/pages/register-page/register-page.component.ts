import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { FloatLabelModule } from 'primeng/floatlabel';
import { InputGroupModule } from 'primeng/inputgroup';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { InputOtpModule } from 'primeng/inputotp';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { SelectModule } from 'primeng/select';
import { StepperModule } from 'primeng/stepper';

type RegisterStep = 1 | 2 | 3;
type DocumentTypeOption = { label: string; value: string };

@Component({
  selector: 'app-register-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ButtonModule,
    CheckboxModule,
    FloatLabelModule,
    InputGroupModule,
    InputGroupAddonModule,
    InputOtpModule,
    InputTextModule,
    PasswordModule,
    SelectModule,
    StepperModule
  ],
  templateUrl: './register-page.component.html',
  styleUrl: './register-page.component.scss'
})
export class RegisterPageComponent {
  private readonly emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
  private readonly nameRegex = /^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+(?:\s+[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+)*$/;

  protected readonly step = signal<RegisterStep>(1);
  protected readonly submitAttempted = signal(false);

  protected readonly businessEmail = signal('');
  protected readonly codeSent = signal(false);
  protected readonly otpCode = signal('');

  protected readonly firstName = signal('');
  protected readonly lastName = signal('');
  protected readonly cedulaType = signal('V');
  protected readonly cedula = signal('');
  protected readonly rifType = signal('J');
  protected readonly rif = signal('');
  protected readonly whatsapp = signal('');
  protected readonly address = signal('');
  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly confirmPassword = signal('');
  protected readonly acceptedTerms = signal(false);

  protected readonly cedulaTypeOptions: DocumentTypeOption[] = [
    { label: 'V', value: 'V' },
    { label: 'E', value: 'E' }
  ];

  protected readonly rifTypeOptions: DocumentTypeOption[] = [
    { label: 'V', value: 'V' },
    { label: 'E', value: 'E' },
    { label: 'J', value: 'J' },
    { label: 'G', value: 'G' },
    { label: 'C', value: 'C' }
  ];

  protected readonly isBusinessEmailValid = computed(() => this.emailRegex.test(this.businessEmail().trim().toLowerCase()));
  protected readonly isOtpValid = computed(() => /^\d{6}$/.test(this.otpCode()));
  protected readonly isFirstNameValid = computed(() => this.isValidName(this.firstName()));
  protected readonly isLastNameValid = computed(() => this.isValidName(this.lastName()));
  protected readonly isCedulaValid = computed(() => /^\d{6,8}$/.test(this.cedula()));
  protected readonly isRifValid = computed(() => this.rif().length === 0 || /^\d{5,9}$/.test(this.rif()));
  protected readonly isWhatsappValid = computed(() => this.whatsapp().trim().length >= 8);
  protected readonly isAddressValid = computed(() => this.address().trim().length >= 10);
  protected readonly isPasswordValid = computed(() => this.password().length >= 8);
  protected readonly isConfirmPasswordValid = computed(() => this.confirmPassword().length > 0 && this.confirmPassword() === this.password());

  protected readonly canSubmit = computed(
    () =>
      this.isFirstNameValid() &&
      this.isLastNameValid() &&
      this.isCedulaValid() &&
      this.isRifValid() &&
      this.isWhatsappValid() &&
      this.isAddressValid() &&
      this.isPasswordValid() &&
      this.isConfirmPasswordValid() &&
      this.acceptedTerms()
  );

  protected readonly fullName = computed(() => `${this.firstName().trim()} ${this.lastName().trim()}`.trim());

  protected updateFirstName(value: string): void {
    this.firstName.set(this.normalizeName(value));
  }

  protected updateLastName(value: string): void {
    this.lastName.set(this.normalizeName(value));
  }

  protected updateCedula(value: string): void {
    this.cedula.set(this.normalizeDigits(value, 8));
  }

  protected updateRif(value: string): void {
    this.rif.set(this.normalizeDigits(value, 9));
  }

  protected sendCode(): void {
    this.submitAttempted.set(true);
    if (!this.isBusinessEmailValid()) {
      return;
    }

    this.codeSent.set(true);
    this.submitAttempted.set(false);
  }

  protected verifyCode(): void {
    this.submitAttempted.set(true);
    if (!this.isBusinessEmailValid() || !this.isOtpValid()) {
      return;
    }

    this.email.set(this.businessEmail().trim());
    this.step.set(2);
    this.submitAttempted.set(false);
  }

  protected goToStepOne(): void {
    this.step.set(1);
    this.submitAttempted.set(false);
  }

  protected submitProfile(): void {
    this.submitAttempted.set(true);
    if (!this.canSubmit()) {
      return;
    }

    this.step.set(3);
    this.submitAttempted.set(false);
  }

  protected resetFlow(): void {
    this.step.set(1);
    this.submitAttempted.set(false);
    this.codeSent.set(false);
    this.businessEmail.set('');
    this.otpCode.set('');
    this.firstName.set('');
    this.lastName.set('');
    this.cedulaType.set('V');
    this.cedula.set('');
    this.rifType.set('J');
    this.rif.set('');
    this.whatsapp.set('');
    this.address.set('');
    this.email.set('');
    this.password.set('');
    this.confirmPassword.set('');
    this.acceptedTerms.set(false);
  }

  private normalizeName(value: string): string {
    return value.replace(/[^A-Za-zÁÉÍÓÚÜÑáéíóúüñ\s]/g, '').replace(/\s{2,}/g, ' ').replace(/^\s+/, '');
  }

  private normalizeDigits(value: string, maxLength: number): string {
    return value.replace(/\D/g, '').slice(0, maxLength);
  }

  private isValidName(value: string): boolean {
    return this.nameRegex.test(value.trim());
  }
}
